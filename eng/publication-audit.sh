#!/usr/bin/env sh
set -eu

HERE=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
ROOT=$(CDPATH='' cd -- "$HERE/.." && pwd)
cd "$ROOT"

fail() {
  echo "Publication audit blocked: $*" >&2
  exit 2
}

require_file() {
  [ -f "$1" ] || fail "required file is missing: $1"
}

echo "[publish 1/11] legal/public docs"
require_file LICENSE
require_file README.md
require_file SECURITY.md
require_file CONTRIBUTING.md
require_file docs/PUBLICATION-CHECKLIST.md
require_file docs/DISTRIBUTION.md
require_file THIRD_PARTY_NOTICES.md
require_file LICENSING.md
require_file reference/nopcommerce/LICENSING.md
LICENSE_EXPR=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageLicenseExpression)
[ "$LICENSE_EXPR" = "Apache-2.0" ] || fail "NuGet PackageLicenseExpression must be Apache-2.0"
grep -Fq 'org.opencontainers.image.licenses="Apache-2.0"' Dockerfile || fail "OCI license metadata must be Apache-2.0"
grep -Fq 'NPL 4.0' reference/nopcommerce/LICENSING.md || fail "nopCommerce licensing boundary must document NPL 4.0"

echo "[publish 2/11] ignored local/generated state"
for path in \
  .artifacts/publication-probe \
  .agent-memory/publication-probe \
  .local/publication-probe \
  dist/publication-probe \
  reference/nopcommerce/.refs/publication-probe.dll \
  reference/nopcommerce/artifacts/publication-probe
do
  git check-ignore -q "$path" || fail "$path is not ignored by Git"
done

echo "[publish 3/11] workflow activation posture"
if [ -d .github/workflows ] && find .github/workflows -type f -print -quit | grep -q .; then
  [ "${PAYTNESS_ENABLE_GITHUB_ACTIONS:-}" = "1" ] || fail ".github/workflows is active without PAYTNESS_ENABLE_GITHUB_ACTIONS=1"
fi

echo "[publish 4/11] publication tree hygiene"
LIST="$ROOT/.artifacts/publication-audit-files.txt"
mkdir -p "$ROOT/.artifacts"
git ls-files --cached --others --exclude-standard > "$LIST"
[ -s "$LIST" ] || fail "no publication candidate files found"

bad_binary=$(grep -Ei '\.(dll|exe|nupkg|snupkg|pdb|so|dylib|tar\.gz)$' "$LIST" || true)
[ -z "$bad_binary" ] || {
  printf '%s\n' "$bad_binary" >&2
  fail "generated/vendor binary artifacts are present in the publication tree"
}

STAGE="$ROOT/.artifacts/publication-audit-tree"
rm -rf "$STAGE"
mkdir -p "$STAGE"
git ls-files --cached --others --exclude-standard -z | tar --null -T - -cf - | tar -C "$STAGE" -xf -

internal_hits=$(grep -RInE '/runtime/home/|/workspace/|/home/[^/[:space:]]+/|[A-Za-z]:\\Users\\[^\\[:space:]]+' "$STAGE" --exclude='publication-audit.sh' 2>/dev/null || true)
[ -z "$internal_hits" ] || {
  printf '%s\n' "$internal_hits" >&2
  fail "private development-environment references remain in publication files"
}

echo "[publish 5/11] release metadata"
./eng/release-metadata-check.sh

echo "[publish 6/11] secret scan"
./eng/secret-scan.sh

echo "[publish 7/11] fast product gate"
./eng/verify.sh

echo "[publish 8/11] controlled performance gate"
./eng/performance-gates.sh

echo "[publish 9/11] reproducible binaries + NuGet"
./eng/package-repro-check.sh

echo "[publish 10/11] real nopCommerce reference"
./reference/nopcommerce/run.sh

echo "[publish 11/11] OCI multiarch + Trivy"
./eng/package-oci.sh

echo "Automatable first-alpha release gates passed."
echo "Source visibility and first-alpha release have separate human gates in docs/PUBLICATION-CHECKLIST.md."
