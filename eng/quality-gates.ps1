$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (-not $env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $root '.artifacts/dotnet-home' }
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME | Out-Null
$project = 'tests/Paytness.QualityGates/Paytness.QualityGates.csproj'
dotnet restore $project --locked-mode
dotnet format $project --verify-no-changes --no-restore
dotnet run --project $project -c Release --no-restore
