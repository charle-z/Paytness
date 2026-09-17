#!/usr/bin/env sh
set -eu

HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ROOT=$(CDPATH= cd -- "$HERE/.." && pwd)
cd "$ROOT"

fail() {
  echo "Publication audit blocked: $*" >&2
  exit 2
}

require_file() {
  [ -f "$1" ] || fail "required file is missing: $1"
}

echo "[publish 1/10] legal/public docs"
require_file LICENSE
require_file README.md
require_file SECURITY.md
require_file CONTRIBUTING.md
require_file docs/PUBLICATION-CHECKLIST.md
require_file docs/DISTRIBUTION.md

echo "[publish 2/10] ignored local/generated state"
for path in \
  .artifacts/publication-probe \
  .local/publication-probe \
  dist/publication-probe \
  reference/nopcommerce/.refs/publication-probe.dll \
  reference/nopcommerce/artifacts/publication-probe
do
  git check-ignore -q "$path" || fail "$path is not ignored by Git"
done

echo "[publish 3/10] workflow activation posture"
if [ -d .github/workflows ] && find .github/workflows -type f -print -quit | grep -q .; then
  [ "${PAYTNESS_ENABLE_GITHUB_ACTIONS:-}" = "1" ] || fail ".github/workflows is active without PAYTNESS_ENABLE_GITHUB_ACTIONS=1"
fi

echo "[publish 4/10] publication tree hygiene"
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

internal_hits=$(grep -RInE 'parrot-flugel|/runtime/home|/workspace/|\.mcp-devbox/' "$STAGE" 2>/dev/null || true)
[ -z "$internal_hits" ] || {
  printf '%s\n' "$internal_hits" >&2
  fail "private development-environment references remain in publication files"
}

echo "[publish 5/10] release metadata"
./eng/release-metadata-check.sh

echo "[publish 6/10] secret scan"
./eng/secret-scan.sh

echo "[publish 7/10] fast product gate"
./eng/verify.sh

echo "[publish 8/10] reproducible binaries + NuGet"
./eng/package-repro-check.sh

echo "[publish 9/10] real nopCommerce reference"
./reference/nopcommerce/run.sh

echo "[publish 10/10] OCI multiarch + Trivy"
./eng/package-oci.sh

echo "Automatable publication gates passed."
echo "Human blocking items in docs/PUBLICATION-CHECKLIST.md still require explicit review before changing visibility or publishing packages."
