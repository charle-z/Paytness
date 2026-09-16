#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"
BIN="src/Paytness/bin/Release/net10.0/paytness.dll"
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT HUP INT TERM

expect_invalid() {
  file=$1
  expected=$2
  set +e
  output=$(dotnet "$BIN" validate "$file" 2>&1)
  code=$?
  set -e
  if [ "$code" -ne 2 ]; then
    echo "Expected exit 2 for $file, got $code: $output" >&2
    exit 1
  fi
  echo "$output" | grep -F "$expected" >/dev/null || {
    echo "Expected '$expected' for $file, got: $output" >&2
    exit 1
  }
}

cat > "$TMP/anchor.yaml" <<'YAML'
version: 1
id: &scenario anchored
contract: contract.yaml
provider:
  responseModes: [deliver]
YAML
expect_invalid "$TMP/anchor.yaml" "YAML anchors are not allowed."

cat > "$TMP/alias.yaml" <<'YAML'
version: 1
id: *missing
contract: contract.yaml
provider:
  responseModes: [deliver]
YAML
expect_invalid "$TMP/alias.yaml" "YAML aliases are not allowed."

cat > "$TMP/tag.yaml" <<'YAML'
version: 1
id: !custom tagged
contract: contract.yaml
provider:
  responseModes: [deliver]
YAML
expect_invalid "$TMP/tag.yaml" "Custom YAML tags are not allowed."

cat > "$TMP/merge.yaml" <<'YAML'
version: 1
id: merge
"<<": value
contract: contract.yaml
provider:
  responseModes: [deliver]
YAML
expect_invalid "$TMP/merge.yaml" "YAML merge keys are not allowed."

printf '%s' '{"version":1,"id":"first","id":"second","contract":"contract.json"}' > "$TMP/duplicate.json"
expect_invalid "$TMP/duplicate.json" "JSON contains duplicate property 'id'."

nested=value; i=0; while [ "$i" -lt 33 ]; do nested="[$nested]"; i=$((i + 1)); done
printf 'version: 1\nid: %s\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\n' "$nested" > "$TMP/deep.yaml"
expect_invalid "$TMP/deep.yaml" "YAML nesting exceeds depth 32."

awk 'BEGIN { printf "["; for (i=0; i<10001; i++) { if (i) printf ","; printf "0" } printf "]" }' > "$TMP/nodes.json"
expect_invalid "$TMP/nodes.json" "JSON exceeds 10000 nodes."

mkdir -p "$TMP/root/contracts" "$TMP/outside"
printf '%s\n' 'version: 1' > "$TMP/outside/basic.yaml"
ln -s "$TMP/outside/basic.yaml" "$TMP/root/contracts/basic.yaml"
printf '%s\n' 'version: 1' 'id: symlink' 'contract: contracts/basic.yaml' 'provider:' '  responseModes: [deliver]' > "$TMP/root/scenario.yaml"
expect_invalid "$TMP/root/scenario.yaml" "Contract path escapes the scenario root."

echo "Security smoke passed."
