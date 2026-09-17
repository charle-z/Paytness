$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$blocks = 0
function Pass([string]$m) { Write-Host "[PASS] $m" }
function Block([string]$m) { Write-Host "[BLOCK] $m"; $script:blocks++ }
function Info([string]$m) { Write-Host "[INFO] $m" }
function External([string]$m) { Write-Host "[EXTERNAL] $m" }
function Manual([string]$m) { Write-Host "[MANUAL] $m" }

Write-Host 'Paytness publication preflight'
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
if ($compose) { Pass 'Docker Compose available for real-SUT gate' } else { External 'Docker Compose unavailable here: run the full nopCommerce gate on a normal Docker host' }
if ($buildx) { Pass 'Docker Buildx available for multiarch OCI gate' } else { External 'Docker Buildx unavailable here: run amd64/arm64 OCI + Trivy on a normal release host' }
External 'Smoke Windows x64 and macOS release binaries on their native OSes'
External 'Have someone other than the author reproduce the clean Quick Start / first useful PASS/FAIL'
Manual 'Re-check then-current nopCommerce NPL 4.0 terms before redistributing any combined/derived reference artifact'
Manual 'Perform final trademark/name review in the official dynamic trademark databases'
Manual 'After repository visibility changes, enable GitHub Private Vulnerability Reporting and verify the Report a vulnerability button before announcing a release'
Write-Host "`nAutomated repository blockers: $blocks"
if ($blocks -ne 0) { exit 1 }
Write-Host 'Repository-local preflight is clear; external/manual publication gates still apply.'
