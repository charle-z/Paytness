#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

fail() {
  echo "Release metadata mismatch: $*" >&2
  exit 1
}

VERSION=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:Version)
PACKAGE_ID=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageId)
REPOSITORY_URL=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:RepositoryUrl)
PROJECT_URL=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageProjectUrl)
REPOSITORY_TYPE=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:RepositoryType)
SDK_VERSION=$(sed -n 's/.*"version": "\([^"]*\)".*/\1/p' global.json | head -n 1)
PLUGIN_VERSION=$(sed -n 's/.*"Version": "\([^"]*\)".*/\1/p' reference/nopcommerce/plugin/Paytness.Reference/plugin.json | head -n 1)
ACTION_VERSION=$(awk '/^  version:/{seen=1; next} seen && /^[[:space:]]+default:/{gsub(/[\047\042]/, "", $2); print $2; exit}' action.yml)
ACTION_SDK=$(awk '/dotnet-version:/{gsub(/[\047\042]/, "", $2); print $2; exit}' action.yml)

[ "$PACKAGE_ID" = "Paytness" ] || fail "PackageId is '$PACKAGE_ID', expected 'Paytness'"
[ "$REPOSITORY_URL" = "https://github.com/charle-z/paytness" ] || fail "RepositoryUrl is not the owner-bound Paytness repository"
[ "$PROJECT_URL" = "$REPOSITORY_URL" ] || fail "PackageProjectUrl does not match RepositoryUrl"
[ "$REPOSITORY_TYPE" = "git" ] || fail "RepositoryType must be git"
[ "$ACTION_VERSION" = "$VERSION" ] || fail "action.yml version '$ACTION_VERSION' != project '$VERSION'"
[ "$PLUGIN_VERSION" = "$VERSION" ] || fail "nopCommerce plugin '$PLUGIN_VERSION' != project '$VERSION'"
[ "$ACTION_SDK" = "$SDK_VERSION" ] || fail "action SDK '$ACTION_SDK' != global.json '$SDK_VERSION'"

grep -Fq "ARG VERSION=$VERSION" Dockerfile || fail "root Dockerfile VERSION default is not $VERSION"
grep -Fq "dotnet/aspnet:10.0.12" Dockerfile || fail "root Dockerfile runtime is not pinned to ASP.NET Core 10.0.12"
expected_runtime_user="USER \$APP_UID"
grep -Fq "$expected_runtime_user" Dockerfile || fail "root Dockerfile does not enforce the non-root runtime user"
if grep -Eq '^[[:space:]]*RUN[[:space:]]' Dockerfile; then fail "root Dockerfile must remain runtime-only and contain no RUN instructions"; fi
grep -Fq "SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:$SDK_VERSION" reference/nopcommerce/build-images.sh || fail "reference build-images SDK does not match global.json"
grep -Fq 'nopcommerceteam/nopcommerce:4.90.8' reference/nopcommerce/Dockerfile.nopcommerce || fail "nopCommerce reference image is not pinned to 4.90.8"
grep -Fq 'powered by nopCommerce' reference/nopcommerce/plugin/Paytness.Reference/ReferencePaymentInfoViewComponent.cs || fail "nopCommerce reference UI attribution is missing"
grep -Fq 'https://www.nopcommerce.com' reference/nopcommerce/plugin/Paytness.Reference/ReferencePaymentInfoViewComponent.cs || fail "nopCommerce reference UI attribution link is missing"
grep -Fq 'dc exec -T nopcommerce wget -qO- http://127.0.0.1:8080/test/state' reference/nopcommerce/run.sh || fail "reference readiness probe must use upstream nopCommerce wget"
if grep -Fq 'dc exec -T paytness curl' reference/nopcommerce/run.sh; then fail "reference runner must not depend on curl in the runtime-only Paytness image"; fi
grep -Fq 'dotnet/aspnet:10.0.12' reference/nopcommerce/Dockerfile.paytness || fail "reference Paytness runtime is not pinned to ASP.NET Core 10.0.12"
for file in reference/nopcommerce/Dockerfile.nopcommerce reference/nopcommerce/Dockerfile.paytness; do
  if grep -Eq '^[[:space:]]*RUN[[:space:]]' "$file"; then fail "$file must remain runtime-only and contain no RUN instructions"; fi
done

printf 'Release metadata aligned: version=%s package=%s sdk=%s nopCommerce=4.90.8\n' "$VERSION" "$PACKAGE_ID" "$SDK_VERSION"
