#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
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
LAYOUT="$DIST/paytness-$VERSION-linux-amd64-arm64.oci"
OUT="$DIST/paytness-$VERSION-linux-amd64-arm64.oci.tar"
rm -rf "$LAYOUT"
rm -f "$OUT"

SOURCE_DATE_EPOCH="$BUILD_EPOCH" docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg "VERSION=$VERSION" \
  --build-arg "REVISION=$REVISION" \
  --output "type=oci,dest=$LAYOUT,tar=false,rewrite-timestamp=true" \
  -f "$ROOT/Dockerfile" \
  "$CONTEXT"

./eng/scan-oci.sh

tar --sort=name --mtime="@$BUILD_EPOCH" --owner=0 --group=0 --numeric-owner -cf "$OUT" -C "$LAYOUT" .
rm -rf "$LAYOUT"

(
  cd "$DIST"
  manifest=$(mktemp)
  find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > "$manifest"
  mv "$manifest" SHA256SUMS
)

printf 'Paytness %s OCI multiarch artifact and vulnerability scan passed: %s\n' "$VERSION" "$OUT"
