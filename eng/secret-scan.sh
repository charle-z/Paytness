#!/usr/bin/env sh
set -eu

HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ROOT=$(CDPATH= cd -- "$HERE/.." && pwd)
cd "$ROOT"

VERSION=8.30.1
case "$(uname -m)" in
  x86_64|amd64)
    ASSET="gitleaks_${VERSION}_linux_x64.tar.gz"
    CHECKSUM="551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb"
    ;;
  aarch64|arm64)
    ASSET="gitleaks_${VERSION}_linux_arm64.tar.gz"
    CHECKSUM="e4a487ee7ccd7d3a7f7ec08657610aa3606637dab924210b3aee62570fb4b080"
    ;;
  *)
    echo "Unsupported maintainer architecture for pinned Gitleaks bootstrap: $(uname -m)" >&2
    exit 2
    ;;
esac

TOOL_DIR="$ROOT/.artifacts/tools/gitleaks/v$VERSION"
BIN="$TOOL_DIR/gitleaks"
ARCHIVE="$TOOL_DIR/$ASSET"
mkdir -p "$TOOL_DIR" "$ROOT/.artifacts"

if [ ! -x "$BIN" ] || [ "$("$BIN" version 2>/dev/null || true)" != "$VERSION" ]; then
  command -v wget >/dev/null 2>&1 || {
    echo "wget is required to bootstrap the pinned Gitleaks binary." >&2
    exit 2
  }
  wget -qO "$ARCHIVE" "https://github.com/gitleaks/gitleaks/releases/download/v${VERSION}/${ASSET}"
  printf '%s  %s\n' "$CHECKSUM" "$ARCHIVE" | sha256sum -c -
  tar -xzf "$ARCHIVE" -C "$TOOL_DIR" gitleaks
  chmod +x "$BIN"
fi

STAGE="$ROOT/.artifacts/publication-tree"
LIST="$ROOT/.artifacts/publication-files.zlist"
REPORT="$ROOT/.artifacts/gitleaks-report.json"
rm -rf "$STAGE"
mkdir -p "$STAGE"

# Scan exactly what would enter Git: tracked files plus untracked files that are
# not ignored. Local caches, dist artifacts and generated reference state stay out.
git ls-files --cached --others --exclude-standard -z > "$LIST"
if [ ! -s "$LIST" ]; then
  echo "No publication candidate files found." >&2
  exit 2
fi

tar --null -T "$LIST" -cf - | tar -C "$STAGE" -xf -
rm -f "$REPORT"

"$BIN" dir \
  --no-banner \
  --no-color \
  --redact \
  --max-target-megabytes 10 \
  --report-format json \
  --report-path "$REPORT" \
  "$STAGE"

echo "Secret scan passed with Gitleaks $VERSION."
echo "Scanned publication candidate tree only; report: .artifacts/gitleaks-report.json"
