#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

if ! command -v docker >/dev/null 2>&1 || ! docker buildx version >/dev/null 2>&1; then
  echo "Paytness OCI packaging requires Docker Buildx." >&2
  exit 2
fi

./eng/release-metadata-check.sh

VERSION=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:Version)
DIST="$ROOT/dist/v$VERSION"
mkdir -p "$DIST"
OUT="$DIST/paytness-$VERSION-linux-amd64-arm64.oci.tar"
rm -f "$OUT"

docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg "VERSION=$VERSION" \
  --output "type=oci,dest=$OUT" \
  .

./eng/scan-oci.sh

(
  cd "$DIST"
  find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS
)

printf 'Paytness %s OCI multiarch artifact and vulnerability scan passed: %s\n' "$VERSION" "$OUT"
