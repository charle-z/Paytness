#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

fail() {
  echo "Release metadata mismatch: $*" >&2
  exit 1
}

VERSION=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:Version)
PACKAGE_ID=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageId)
SDK_VERSION=$(sed -n 's/.*"version": "\([^"]*\)".*/\1/p' global.json | head -n 1)
PLUGIN_VERSION=$(sed -n 's/.*"Version": "\([^"]*\)".*/\1/p' reference/nopcommerce/plugin/Paytness.Reference/plugin.json | head -n 1)
ACTION_VERSION=$(awk '/^  version:/{seen=1; next} seen && /^[[:space:]]+default:/{gsub(/[\047\042]/, "", $2); print $2; exit}' action.yml)
ACTION_SDK=$(awk '/dotnet-version:/{gsub(/[\047\042]/, "", $2); print $2; exit}' action.yml)

[ "$PACKAGE_ID" = "Paytness" ] || fail "PackageId is '$PACKAGE_ID', expected 'Paytness'"
[ "$ACTION_VERSION" = "$VERSION" ] || fail "action.yml version '$ACTION_VERSION' != project '$VERSION'"
[ "$PLUGIN_VERSION" = "$VERSION" ] || fail "nopCommerce plugin '$PLUGIN_VERSION' != project '$VERSION'"
[ "$ACTION_SDK" = "$SDK_VERSION" ] || fail "action SDK '$ACTION_SDK' != global.json '$SDK_VERSION'"

grep -Fq "ARG VERSION=$VERSION" Dockerfile || fail "root Dockerfile VERSION default is not $VERSION"
grep -Fq "dotnet/sdk:$SDK_VERSION" Dockerfile || fail "root Dockerfile SDK does not match global.json"
grep -Fq "dotnet/sdk:$SDK_VERSION" reference/nopcommerce/Dockerfile.nopcommerce || fail "reference plugin Dockerfile SDK does not match global.json"
grep -Fq "dotnet/sdk:$SDK_VERSION" reference/nopcommerce/Dockerfile.paytness || fail "reference Paytness Dockerfile SDK does not match global.json"
grep -Fq 'nopcommerceteam/nopcommerce:4.90.8' reference/nopcommerce/Dockerfile.nopcommerce || fail "nopCommerce reference image is not pinned to 4.90.8"

printf 'Release metadata aligned: version=%s package=%s sdk=%s nopCommerce=4.90.8\n' "$VERSION" "$PACKAGE_ID" "$SDK_VERSION"
