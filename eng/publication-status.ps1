$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$blocks = 0
function Pass([string]$m) { Write-Host "[PASS] $m" }
function Block([string]$m) { Write-Host "[BLOCK] $m"; $script:blocks++ }
function Info([string]$m) { Write-Host "[INFO] $m" }
function Alpha([string]$m) { Write-Host "[ALPHA] $m" }
function Manual([string]$m) { Write-Host "[MANUAL] $m" }

Write-Host 'Paytness public-source preview preflight'
Write-Host ''
foreach ($required in @('README.md','SECURITY.md','CONTRIBUTING.md','LICENSE','LICENSING.md','THIRD_PARTY_NOTICES.md','reference/nopcommerce/LICENSING.md','docs/PUBLICATION-CHECKLIST.md','docs/DISTRIBUTION.md')) {
    if (Test-Path $required -PathType Leaf) { Pass "$required present" } else { Block "$required missing" }
}
if ((Test-Path 'LICENSE' -PathType Leaf) -and ((Get-Content LICENSE -Raw) -match 'Apache License') -and ((Get-Content LICENSE -Raw) -match 'Version 2.0')) { Pass 'Apache-2.0 LICENSE present' } else { Block 'LICENSE is missing or is not Apache License 2.0' }

try {
    $licenseExpr = (& dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageLicenseExpression).Trim()
    $licenseFile = (& dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageLicenseFile).Trim()
    $repoUrl = (& dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:RepositoryUrl).Trim()
    if ($licenseExpr -eq 'Apache-2.0') { Pass 'NuGet license metadata is Apache-2.0' } else { Block "unexpected NuGet license metadata: $licenseExpr" }
    if ($repoUrl -eq 'https://github.com/charle-z/paytness') { Pass 'NuGet RepositoryUrl matches owner-bound repository' } else { Block "unexpected RepositoryUrl: $repoUrl" }
} catch { Block '.NET SDK unavailable: cannot inspect package metadata' }

if ((Get-Content Dockerfile -Raw) -match 'org.opencontainers.image.licenses="Apache-2.0"') { Pass 'OCI license metadata is Apache-2.0' } else { Block 'OCI license metadata is not Apache-2.0' }
if (((Get-Content reference/nopcommerce/LICENSING.md -Raw) -match 'NPL 4.0') -and ((Get-Content reference/nopcommerce/LICENSING.md -Raw) -match 'does \*\*not\*\* publish a prebuilt nopCommerce-derived image')) { Pass 'nopCommerce NPL 4.0 reference boundary documented' } else { Block 'nopCommerce reference licensing boundary is incomplete' }

if ((Get-Content SECURITY.md -Raw) -match 'GitHub Private Vulnerability Reporting') { Pass 'SECURITY.md defines the private disclosure launch path' } else { Block 'SECURITY.md does not define GitHub Private Vulnerability Reporting' }
if ((Test-Path '.github/workflows') -and (Get-ChildItem '.github/workflows' -File -ErrorAction SilentlyContinue | Select-Object -First 1)) { Info 'active GitHub workflows are present; verify Actions budget posture intentionally' } else { Pass 'repository workflows remain inactive in pre-alpha' }
if (-not (git status --porcelain)) { Pass 'Git working tree is clean' } else { Info 'Git working tree is dirty (release candidate must be clean)' }

$compose = $false; $buildx = $false
try { docker compose version *> $null; $compose = $LASTEXITCODE -eq 0 } catch {}
try { docker buildx version *> $null; $buildx = $LASTEXITCODE -eq 0 } catch {}
if ($compose) { Alpha 'Docker Compose available for the first-alpha real-SUT gate' } else { Alpha 'Docker Compose unavailable here; first packaged alpha still requires the full nopCommerce gate on a normal Docker host' }
if ($buildx) { Alpha 'Docker Buildx available for the first-alpha multiarch OCI gate' } else { Alpha 'Docker Buildx unavailable here; first packaged alpha still requires amd64/arm64 OCI + Trivy on a normal release host' }
Alpha 'Smoke native release binaries before advertising their first packaged alpha surfaces'
Alpha 'Have someone other than the author attempt the clean Quick Start before the first packaged alpha'
Alpha 'Re-check then-current nopCommerce NPL 4.0 terms before redistributing any combined/derived reference artifact'
Manual 'Complete the official trademark-database screen before changing repository visibility'
Manual 'After repository visibility changes, immediately enable GitHub Private Vulnerability Reporting and verify Report a vulnerability before announcing the source preview'
Write-Host "`nAutomated source-preview blockers: $blocks"
if ($blocks -ne 0) { exit 1 }
Write-Host 'Repository-local source-preview preflight is clear; MANUAL visibility checks and ALPHA release gates still apply.'
