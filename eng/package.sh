#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT/.artifacts/dotnet-home}"
mkdir -p "$DOTNET_CLI_HOME"

VERSION=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:Version)
[ -n "$VERSION" ] || { echo "Could not resolve Paytness version." >&2; exit 2; }
BUILD_EPOCH=$(git log -1 --format=%ct)
[ -n "$BUILD_EPOCH" ] || { echo "Packaging requires a Git commit timestamp." >&2; exit 2; }
export SOURCE_DATE_EPOCH="$BUILD_EPOCH"

DIST="$ROOT/dist/v$VERSION"
PUBLISH_ROOT="$ROOT/.artifacts/publish"
LOCK_ROOT="$ROOT/.artifacts/rid-locks"
TOOL_ROOT="$ROOT/.artifacts/tool-smoke"
SMOKE_ROOT="$ROOT/.artifacts/package-smoke"
CANONICAL_LOCK="$ROOT/src/Paytness/packages.lock.json"
RIDS="linux-x64 linux-arm64 win-x64 osx-x64 osx-arm64"

rm -rf "$DIST" "$PUBLISH_ROOT" "$LOCK_ROOT" "$TOOL_ROOT" "$SMOKE_ROOT"
mkdir -p "$DIST" "$PUBLISH_ROOT" "$LOCK_ROOT" "$TOOL_ROOT" "$SMOKE_ROOT"

canonical_before=$(sha256sum "$CANONICAL_LOCK" | awk '{print $1}')

for rid in $RIDS; do
  echo "[package] publishing $rid"
  rid_lock="$LOCK_ROOT/$rid/packages.lock.json"
  out="$PUBLISH_ROOT/$rid"
  mkdir -p "$(dirname "$rid_lock")" "$out"

  dotnet restore src/Paytness/Paytness.csproj -r "$rid" --force-evaluate \
    -p:NuGetLockFilePath="$rid_lock" \
    -p:RestorePackagesWithLockFile=true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:SelfContained=true >/dev/null

  dotnet publish src/Paytness/Paytness.csproj -c Release -r "$rid" --self-contained true --no-restore \
    -p:NuGetLockFilePath="$rid_lock" \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:ContinuousIntegrationBuild=true \
    -o "$out" >/dev/null

  count=$(find "$out" -maxdepth 1 -type f | wc -l)
  [ "$count" -eq 1 ] || { echo "$rid did not publish as exactly one file." >&2; exit 1; }

  bundle="$PUBLISH_ROOT/bundle-$rid"
  rm -rf "$bundle"
  mkdir -p "$bundle"
  cp -p "$out"/* "$bundle/"
  cp LICENSE LICENSING.md THIRD_PARTY_NOTICES.md "$bundle/"
  archive="$DIST/paytness-$VERSION-$rid.tar.gz"
  tar --sort=name --mtime="@$BUILD_EPOCH" --owner=0 --group=0 --numeric-owner -cf - -C "$bundle" . \
    | gzip -n > "$archive"
done

canonical_after=$(sha256sum "$CANONICAL_LOCK" | awk '{print $1}')
[ "$canonical_before" = "$canonical_after" ] || {
  echo "Packaging modified the canonical NuGet lock file." >&2
  exit 1
}

cat > "$SMOKE_ROOT/contract.json" <<'JSON'
{"version":1}
JSON
cat > "$SMOKE_ROOT/scenario.json" <<'JSON'
{"version":1,"id":"isolated-package-smoke","contract":"contract.json","provider":{"responseModes":["deliver"]}}
JSON

echo "[package] smoke linux-x64 in isolated directory"
(
  cd "$SMOKE_ROOT"
  "$PUBLISH_ROOT/linux-x64/paytness" --version | grep -Fx "$VERSION" >/dev/null
  "$PUBLISH_ROOT/linux-x64/paytness" validate scenario.json >/dev/null
)

echo "[package] NuGet tool"
NUGET_DIR="$DIST/nuget"
mkdir -p "$NUGET_DIR"
dotnet pack src/Paytness/Paytness.csproj -c Release --no-restore -o "$NUGET_DIR" \
  -p:Deterministic=true -p:DeterministicTimestamp="$BUILD_EPOCH" >/dev/null

cat > "$TOOL_ROOT/NuGet.Config" <<CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$NUGET_DIR" />
  </packageSources>
</configuration>
CONFIG

dotnet tool install --tool-path "$TOOL_ROOT/install" Paytness --version "$VERSION" --configfile "$TOOL_ROOT/NuGet.Config" >/dev/null
(
  cd "$SMOKE_ROOT"
  "$TOOL_ROOT/install/paytness" --version | grep -Fx "$VERSION" >/dev/null
  "$TOOL_ROOT/install/paytness" validate scenario.json >/dev/null
)

(
  cd "$DIST"
  manifest=$(mktemp)
  find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > "$manifest"
  mv "$manifest" SHA256SUMS
)

printf 'Paytness %s packaging passed.\nArtifacts: %s\n' "$VERSION" "$DIST"
