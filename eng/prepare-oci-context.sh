#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

VERSION=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:Version)
BUILD_EPOCH=$(git log -1 --format=%ct)
REVISION=$(git rev-parse HEAD)
[ -n "$VERSION" ] || { echo "Could not resolve Paytness version." >&2; exit 2; }
[ -n "$BUILD_EPOCH" ] || { echo "OCI packaging requires a Git commit timestamp." >&2; exit 2; }
[ -n "$REVISION" ] || { echo "OCI packaging requires a Git revision." >&2; exit 2; }

CONTEXT="$ROOT/.artifacts/oci-context"
rm -rf "$CONTEXT"
mkdir -p "$CONTEXT/app"

export SOURCE_DATE_EPOCH="$BUILD_EPOCH"
dotnet restore src/Paytness/Paytness.csproj --locked-mode >/dev/null
dotnet publish src/Paytness/Paytness.csproj -c Release --no-restore --self-contained false \
  -p:UseAppHost=false \
  -p:Version="$VERSION" \
  -p:ContinuousIntegrationBuild=true \
  -o "$CONTEXT/app" >/dev/null

# BuildKit rewrites layer timestamps from SOURCE_DATE_EPOCH. Normalizing the
# staged payload too keeps local/non-BuildKit inspection deterministic.
find "$CONTEXT/app" -type f -exec touch -d "@$BUILD_EPOCH" {} +

printf 'context=%s\nversion=%s\nrevision=%s\nepoch=%s\n' "$CONTEXT" "$VERSION" "$REVISION" "$BUILD_EPOCH"
