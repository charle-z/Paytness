$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $root '.artifacts/dotnet-home' }
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME | Out-Null

try { $sdk = (& dotnet --version).Trim() } catch { $sdk = '' }
if (-not $sdk) { throw 'Paytness requires the .NET 10 SDK. Install the SDK and rerun eng/verify.ps1.' }
if (-not $sdk.StartsWith('10.')) { throw "Paytness requires .NET SDK 10.x; found $sdk." }

Write-Host '[1/8] restore locked'
dotnet restore Paytness.sln --locked-mode
Write-Host '[2/8] format verify'
dotnet format Paytness.sln --verify-no-changes --no-restore
Write-Host '[3/8] release build'
dotnet build Paytness.sln -c Release --no-restore
Write-Host '[4/8] tests'
dotnet test Paytness.sln -c Release --no-build
Write-Host '[5/8] deterministic quality gates'
& (Join-Path $PSScriptRoot 'quality-gates.ps1')
Write-Host '[6/8] scenario contract smoke'
dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate scenarios/healthy.yaml
dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate scenarios/duplicate-webhook.yaml
dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate scenarios/out-of-order-webhook.yaml
dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate scenarios/concurrent-same-payment.yaml
Get-ChildItem 'scenarios/nopcommerce/nop-*.yaml' | Sort-Object Name | ForEach-Object {
    dotnet src/Paytness/bin/Release/net10.0/paytness.dll validate $_.FullName
}
Write-Host '[7/8] adversarial security smoke'
& (Join-Path $PSScriptRoot 'security-smoke.ps1')
Write-Host '[8/8] dependency vulnerability audit'
dotnet list Paytness.sln package --vulnerable --include-transitive
Write-Host 'Paytness verification passed.'
