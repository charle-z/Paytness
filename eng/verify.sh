#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT/.artifacts/dotnet-home}"
mkdir -p "$DOTNET_CLI_HOME"

required_sdk=$(dotnet --version 2>/dev/null || true)
if [ -z "$required_sdk" ]; then
  echo "Paytness requires the .NET 10 SDK. Install the SDK and rerun eng/verify.sh." >&2
  exit 2
fi

case "$required_sdk" in
  10.*) ;;
  *) echo "Paytness requires .NET SDK 10.x; found $required_sdk." >&2; exit 2 ;;
esac

echo "[1/8] restore locked"
dotnet restore Paytness.sln --locked-mode

echo "[2/8] format verify"
dotnet format Paytness.sln --verify-no-changes --no-restore

echo "[3/8] release build"
dotnet build Paytness.sln -c Release --no-restore

echo "[4/8] tests"
dotnet test Paytness.sln -c Release --no-build

echo "[5/8] deterministic quality gates"
./eng/quality-gates.sh

echo "[6/8] scenario contract smoke"
dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate scenarios/healthy.yaml
dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate scenarios/duplicate-webhook.yaml
dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate scenarios/out-of-order-webhook.yaml
dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate scenarios/concurrent-same-payment.yaml
for scenario in scenarios/nopcommerce/nop-*.yaml; do
  dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate "$scenario"
done

echo "[7/8] adversarial security smoke"
./eng/security-smoke.sh

echo "[8/8] dependency vulnerability audit"
dotnet list Paytness.sln package --vulnerable --include-transitive

echo "Paytness verification passed."
