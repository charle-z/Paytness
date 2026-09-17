#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

TRIVY_VERSION=0.74.0
case "$(uname -m)" in
  x86_64|amd64)
    ASSET="trivy_${TRIVY_VERSION}_Linux-64bit.tar.gz"
    CHECKSUM="2ae6fe3ee734b7fdf11335663e18c75ea12dccc76062f09f164a3b0f8be4371a"
    ;;
  aarch64|arm64)
    ASSET="trivy_${TRIVY_VERSION}_Linux-ARM64.tar.gz"
    CHECKSUM="b94ce1976bbf3c15b514b605ee88be7c6d94a29be2302847ff01cb794d47aad5"
    ;;
  *)
    echo "Unsupported maintainer architecture for pinned Trivy bootstrap: $(uname -m)" >&2
    exit 2
    ;;
esac

VERSION=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:Version)
DIST="$ROOT/dist/v$VERSION"
OCI="$DIST/paytness-$VERSION-linux-amd64-arm64.oci"
[ -d "$OCI" ] || {
  echo "OCI layout is missing: $OCI" >&2
  echo "Run ./eng/package-oci.sh first on a Docker Buildx host." >&2
  exit 2
}

TOOL_DIR="$ROOT/.artifacts/tools/trivy/v$TRIVY_VERSION"
BIN="$TOOL_DIR/trivy"
ARCHIVE="$TOOL_DIR/$ASSET"
CACHE="$ROOT/.artifacts/trivy-cache"
mkdir -p "$TOOL_DIR" "$CACHE" "$DIST/security"

if [ ! -x "$BIN" ] || ! "$BIN" --version 2>/dev/null | grep -q "Version: $TRIVY_VERSION"; then
  command -v wget >/dev/null 2>&1 || {
    echo "wget is required to bootstrap the pinned Trivy binary." >&2
    exit 2
  }
  wget -qO "$ARCHIVE" "https://github.com/aquasecurity/trivy/releases/download/v${TRIVY_VERSION}/${ASSET}"
  printf '%s  %s\n' "$CHECKSUM" "$ARCHIVE" | sha256sum -c -
  tar -xzf "$ARCHIVE" -C "$TOOL_DIR" trivy
  chmod +x "$BIN"
fi

scan_platform() {
  platform="$1"
  label=$(printf '%s' "$platform" | tr '/' '-')
  report="$DIST/security/trivy-$label.json"
  echo "[trivy] scanning $platform for HIGH/CRITICAL vulnerabilities"
  set +e
  "$BIN" image \
    --cache-dir "$CACHE" \
    --input "$OCI" \
    --platform "$platform" \
    --scanners vuln \
    --severity HIGH,CRITICAL \
    --exit-code 1 \
    --format json \
    --output "$report"
  code=$?
  set -e
  if [ "$code" -ne 0 ]; then
    echo "Trivy blocked $platform. Review: ${report#$ROOT/}" >&2
    return "$code"
  fi
  echo "[trivy] $platform passed. Report: ${report#$ROOT/}"
}

amd64=0
arm64=0
scan_platform linux/amd64 || amd64=$?
scan_platform linux/arm64 || arm64=$?

if [ "$amd64" -ne 0 ] || [ "$arm64" -ne 0 ]; then
  echo "OCI vulnerability scan failed (amd64=$amd64 arm64=$arm64)." >&2
  exit 1
fi

printf 'Paytness %s OCI vulnerability scan passed with Trivy %s.\n' "$VERSION" "$TRIVY_VERSION"
