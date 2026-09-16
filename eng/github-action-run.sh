#!/usr/bin/env sh
set -eu

: "${PAYTNESS_ACTION_SCENARIO:?scenario input is required}"
: "${PAYTNESS_ACTION_SUT:?sut input is required}"

VERSION=${PAYTNESS_ACTION_VERSION:-0.1.0}
SOURCE=${PAYTNESS_ACTION_SOURCE:-https://api.nuget.org/v3/index.json}
PROVIDER_LISTEN=${PAYTNESS_ACTION_PROVIDER_LISTEN:-127.0.0.1:8787}
JSON_REPORT=${PAYTNESS_ACTION_JSON:-paytness-report.json}
JUNIT_REPORT=${PAYTNESS_ACTION_JUNIT:-paytness-junit.xml}
TEMP_ROOT=${RUNNER_TEMP:-.artifacts/action-temp}
TOOL_ROOT="$TEMP_ROOT/paytness-$VERSION"

case "$JSON_REPORT$JUNIT_REPORT" in
  *'
'*) echo "Report paths must not contain newlines." >&2; exit 2 ;;
esac

rm -rf "$TOOL_ROOT"
mkdir -p "$TOOL_ROOT" "$(dirname "$JSON_REPORT")" "$(dirname "$JUNIT_REPORT")"

dotnet tool install --tool-path "$TOOL_ROOT" Paytness --version "$VERSION" --add-source "$SOURCE" --ignore-failed-sources >/dev/null
TOOL="$TOOL_ROOT/paytness"
[ -x "$TOOL" ] || TOOL="$TOOL_ROOT/paytness.exe"
[ -e "$TOOL" ] || { echo "Installed Paytness command was not found." >&2; exit 3; }

"$TOOL" validate "$PAYTNESS_ACTION_SCENARIO"

set -- "$TOOL" run "$PAYTNESS_ACTION_SCENARIO" \
  --sut "$PAYTNESS_ACTION_SUT" \
  --provider-listen "$PROVIDER_LISTEN" \
  --json "$JSON_REPORT" \
  --junit "$JUNIT_REPORT"

if [ -n "${PAYTNESS_ACTION_ALLOW_TARGET:-}" ]; then
  set -- "$@" --allow-target "$PAYTNESS_ACTION_ALLOW_TARGET"
fi
if [ "${PAYTNESS_ACTION_ALLOW_PUBLIC_TARGETS:-false}" = "true" ]; then
  set -- "$@" --allow-public-targets
fi
if [ "${PAYTNESS_ACTION_ALLOW_NON_LOOPBACK_LISTEN:-false}" = "true" ]; then
  set -- "$@" --allow-non-loopback-listen
fi
if [ -n "${PAYTNESS_ACTION_WEBHOOK_SECRET_ENV:-}" ]; then
  set -- "$@" --webhook-secret-env "$PAYTNESS_ACTION_WEBHOOK_SECRET_ENV"
fi

"$@"

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  printf 'json-report=%s\n' "$JSON_REPORT" >> "$GITHUB_OUTPUT"
  printf 'junit-report=%s\n' "$JUNIT_REPORT" >> "$GITHUB_OUTPUT"
fi
