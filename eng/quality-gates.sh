#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT/.artifacts/dotnet-home}"
mkdir -p "$DOTNET_CLI_HOME"

PROJECT=tests/Paytness.QualityGates/Paytness.QualityGates.csproj

dotnet restore "$PROJECT" --locked-mode
dotnet format "$PROJECT" --verify-no-changes --no-restore
dotnet run --project "$PROJECT" -c Release --no-restore
