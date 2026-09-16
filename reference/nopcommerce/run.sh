#!/usr/bin/env sh
set -eu

HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ROOT=$(CDPATH= cd -- "$HERE/../.." && pwd)
COMPOSE_FILE="$HERE/compose.yaml"

if ! command -v docker >/dev/null 2>&1 || ! docker compose version >/dev/null 2>&1; then
  echo "Paytness nopCommerce reference requires Docker with the Compose plugin." >&2
  exit 2
fi

dc() {
  docker compose -f "$COMPOSE_FILE" "$@"
}

export PAYTNESS_WEBHOOK_SECRET="${PAYTNESS_WEBHOOK_SECRET:-paytness-reference-test-only-signing-key}"
export PAYTNESS_REFERENCE_MUTATION=""
mkdir -p "$ROOT/.artifacts/nopcommerce"

cleanup() {
  if [ "${PAYTNESS_KEEP_REFERENCE:-0}" != "1" ]; then
    dc down -v --remove-orphans >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT INT TERM

wait_ready() {
  i=0
  while [ "$i" -lt 90 ]; do
    if dc exec -T paytness curl -fsS http://nopcommerce:8080/test/state >/dev/null 2>&1; then
      return 0
    fi
    i=$((i + 1))
    sleep 1
  done
  echo "nopCommerce did not become ready." >&2
  dc logs --no-color nopcommerce >&2 || true
  return 1
}

start_clean_stack() {
  mutation="$1"
  export PAYTNESS_REFERENCE_MUTATION="$mutation"
  dc down -v --remove-orphans >/dev/null 2>&1 || true
  dc up -d --no-build postgres paytness nopcommerce
  wait_ready
}

run_scenario() {
  scenario="$1"
  report="$2"
  dc exec -T paytness dotnet /app/paytness.dll run "/scenarios/nopcommerce/$scenario" \
    --sut http://nopcommerce:8080 \
    --provider-listen 0.0.0.0:8787 \
    --allow-non-loopback-listen \
    --allow-target nopcommerce:8080 \
    --webhook-secret-env PAYTNESS_WEBHOOK_SECRET \
    --json "/artifacts/$report.json" \
    --junit "/artifacts/$report.junit.xml"
}

expect_mutation_failure() {
  scenario="$1"
  report="$2"
  needle="$3"
  set +e
  output=$(run_scenario "$scenario" "$report" 2>&1)
  code=$?
  set -e
  printf '%s\n' "$output"
  if [ "$code" -ne 1 ]; then
    echo "Mutation gate unexpectedly returned exit $code; expected 1." >&2
    return 1
  fi
  printf '%s\n' "$output" | grep -F "$needle" >/dev/null || {
    echo "Mutation failed, but not for the required invariant: $needle" >&2
    return 1
  }
}

echo "[reference 1/4] build images"
dc build

echo "[reference 2/4] NOP-01..07"
start_clean_stack ""
for scenario in \
  nop-01-healthy.yaml \
  nop-02-response-lost.yaml \
  nop-03-stable-retry.yaml \
  nop-04-duplicate-webhook.yaml \
  nop-05-out-of-order.yaml \
  nop-06-eventual-convergence.yaml \
  nop-07-concurrent.yaml
do
  report=${scenario%.yaml}
  echo "--- $report ---"
  run_scenario "$scenario" "$report"
done

echo "[reference 3/4] unstable-idempotency mutation"
start_clean_stack "unstable-idempotency"
expect_mutation_failure \
  nop-03-stable-retry.yaml \
  mutation-unstable-idempotency \
  "[Fail] one-economic-effect: expected=1 actual=2"

echo "[reference 4/4] accept-stale-state mutation"
start_clean_stack "accept-stale-state"
expect_mutation_failure \
  nop-05-out-of-order.yaml \
  mutation-accept-stale-state \
  "[Fail] paid: expected=Paid actual=Pending"

export PAYTNESS_REFERENCE_MUTATION=""
echo "Paytness nopCommerce reference passed: NOP-01..07 green and both required mutations detected."
