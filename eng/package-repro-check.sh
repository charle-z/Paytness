#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

VERSION=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:Version)
[ -n "$VERSION" ] || { echo "Could not resolve Paytness version." >&2; exit 2; }

PROBE="$ROOT/.artifacts/package-repro"
FIRST="$PROBE/SHA256SUMS.first"
SECOND="$PROBE/SHA256SUMS.second"
rm -rf "$PROBE"
mkdir -p "$PROBE"

echo "[repro 1/2] first package build"
./eng/package.sh
cp "$ROOT/dist/v$VERSION/SHA256SUMS" "$FIRST"

echo "[repro 2/2] second package build"
./eng/package.sh
cp "$ROOT/dist/v$VERSION/SHA256SUMS" "$SECOND"

if ! diff -u "$FIRST" "$SECOND"; then
  echo "Paytness packaging is not bit-reproducible for the current commit." >&2
  exit 1
fi

echo "Paytness packaging is bit-reproducible for version $VERSION."
