#!/usr/bin/env sh
set -eu

HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ROOT=$(CDPATH= cd -- "$HERE/../.." && pwd)
CLI=${PAYTNESS_CONTAINER_CLI:-docker}
SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.401
NOPCOMMERCE_IMAGE=nopcommerceteam/nopcommerce:4.90.8
NOP_IMAGE=paytness-reference-nopcommerce:local
RUNNER_IMAGE=paytness-reference-runner:local
STAGE="$ROOT/.artifacts/nopcommerce-build"
REFS="$STAGE/refs"
PLUGIN_SRC="$STAGE/plugin-src"
PLUGIN_OUT="$STAGE/plugin-out"
PAY_SRC="$STAGE/paytness-src"
PAY_OUT="$STAGE/paytness-out"
NOP_CONTEXT="$STAGE/nopcommerce-context"
PAY_CONTEXT="$STAGE/paytness-context"

command -v "$CLI" >/dev/null 2>&1 || {
  echo "Paytness nopCommerce reference requires Docker." >&2
  exit 2
}

rm -rf "$STAGE"
mkdir -p "$REFS" "$PLUGIN_SRC" "$PLUGIN_OUT" "$PAY_SRC/src" "$PAY_OUT" \
  "$NOP_CONTEXT/plugin" "$NOP_CONTEXT/runtime" "$PAY_CONTEXT/app" "$PAY_CONTEXT/scenarios"

if ! "$CLI" image inspect "$NOPCOMMERCE_IMAGE" >/dev/null 2>&1; then
  "$CLI" pull "$NOPCOMMERCE_IMAGE" >/dev/null
fi

cid=$("$CLI" create "$NOPCOMMERCE_IMAGE")
cleanup_container() { "$CLI" rm -f "$cid" >/dev/null 2>&1 || true; }
trap cleanup_container EXIT INT TERM
for dll in Nop.Core.dll Nop.Data.dll Nop.Services.dll Nop.Web.Framework.dll; do
  "$CLI" cp "$cid:/app/$dll" "$REFS/$dll"
done
cleanup_container
trap - EXIT INT TERM

cp "$HERE/plugin/Paytness.Reference/Paytness.Reference.csproj" "$PLUGIN_SRC/"
cp "$HERE/plugin/Paytness.Reference/packages.lock.json" "$PLUGIN_SRC/"
cp "$HERE/plugin/Paytness.Reference/plugin.json" "$PLUGIN_SRC/"
cp "$HERE/plugin/Paytness.Reference/"*.cs "$PLUGIN_SRC/"

# Keep the SDK container's outputs owned by the invoking developer on normal Docker hosts.
uid=$(id -u)
gid=$(id -g)
run_sdk() {
  workdir="$1"
  shift
  if [ "$(basename "$CLI")" = "podman" ]; then
    "$CLI" run --rm --network=host --uts=host --user "$uid:$gid" \
      -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp/dotnet \
      -v "$STAGE:/work" -w "$workdir" "$SDK_IMAGE" "$@"
  else
    "$CLI" run --rm --user "$uid:$gid" \
      -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp/dotnet \
      -v "$STAGE:/work" -w "$workdir" "$SDK_IMAGE" "$@"
  fi
}

run_sdk /work/plugin-src sh -lc \
  'dotnet restore Paytness.Reference.csproj --locked-mode -p:NopReferenceDir=/work/refs && dotnet build Paytness.Reference.csproj -c Release --no-restore -p:NopReferenceDir=/work/refs -o /work/plugin-out'

# Copy only source inputs, never developer bin/obj state.
cp "$ROOT/Directory.Build.props" "$ROOT/global.json" "$ROOT/README.md" "$PAY_SRC/"
tar --exclude='src/Paytness/bin' --exclude='src/Paytness/obj' -cf - -C "$ROOT" src/Paytness | tar -xf - -C "$PAY_SRC"

run_sdk /work/paytness-src sh -lc \
  'dotnet restore src/Paytness/Paytness.csproj --locked-mode && dotnet publish src/Paytness/Paytness.csproj -c Release --no-restore --self-contained false -p:UseAppHost=false -p:ContinuousIntegrationBuild=true -o /work/paytness-out'

cp "$PLUGIN_OUT/Paytness.Reference.dll" "$NOP_CONTEXT/plugin/"
cp "$HERE/plugin/Paytness.Reference/plugin.json" "$NOP_CONTEXT/plugin/"
cp "$HERE/runtime/appsettings.json" "$HERE/runtime/plugins.json" "$NOP_CONTEXT/runtime/"
cp "$HERE/Dockerfile.nopcommerce" "$NOP_CONTEXT/Dockerfile"

cp -a "$PAY_OUT/." "$PAY_CONTEXT/app/"
cp -a "$ROOT/scenarios/." "$PAY_CONTEXT/scenarios/"
cp "$HERE/Dockerfile.paytness" "$PAY_CONTEXT/Dockerfile"

build_image() {
  tag="$1"
  context="$2"
  if [ "$(basename "$CLI")" = "podman" ]; then
    "$CLI" build --network=host --uts=host -t "$tag" "$context"
  else
    "$CLI" build -t "$tag" "$context"
  fi
}

build_image "$NOP_IMAGE" "$NOP_CONTEXT"
build_image "$RUNNER_IMAGE" "$PAY_CONTEXT"

printf 'Prepared local reference images:\n  %s\n  %s\n' "$NOP_IMAGE" "$RUNNER_IMAGE"
