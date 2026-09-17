#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

if ! command -v docker >/dev/null 2>&1 || ! docker buildx version >/dev/null 2>&1; then
  echo "Paytness OCI packaging requires Docker Buildx." >&2
  exit 2
fi

./eng/release-metadata-check.sh
./eng/prepare-oci-context.sh >/dev/null

VERSION=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:Version)
BUILD_EPOCH=$(git log -1 --format=%ct)
REVISION=$(git rev-parse HEAD)
CONTEXT="$ROOT/.artifacts/oci-context"
DIST="$ROOT/dist/v$VERSION"
mkdir -p "$DIST"
OUT="$DIST/paytness-$VERSION-linux-amd64-arm64.oci.tar"
rm -f "$OUT"

SOURCE_DATE_EPOCH="$BUILD_EPOCH" docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg "VERSION=$VERSION" \
  --build-arg "REVISION=$REVISION" \
  --output "type=oci,dest=$OUT,rewrite-timestamp=true" \
  -f "$ROOT/Dockerfile" \
  "$CONTEXT"

./eng/scan-oci.sh

(
  cd "$DIST"
  find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS
)

printf 'Paytness %s OCI multiarch artifact and vulnerability scan passed: %s\n' "$VERSION" "$OUT"
