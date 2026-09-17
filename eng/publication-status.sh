#!/usr/bin/env sh
set -u

ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT" || { echo "Cannot enter repository root: $ROOT" >&2; exit 2; }

blocks=0
pass() { printf '[PASS] %s\n' "$*"; }
block() { printf '[BLOCK] %s\n' "$*"; blocks=$((blocks + 1)); }
info() { printf '[INFO] %s\n' "$*"; }
alpha() { printf '[ALPHA] %s\n' "$*"; }
manual() { printf '[MANUAL] %s\n' "$*"; }

printf 'Paytness public-source preview preflight\n\n'

for required in README.md SECURITY.md CONTRIBUTING.md LICENSE LICENSING.md THIRD_PARTY_NOTICES.md reference/nopcommerce/LICENSING.md docs/PUBLICATION-CHECKLIST.md docs/DISTRIBUTION.md; do
  if [ -f "$required" ]; then pass "$required present"; else block "$required missing"; fi
done

if grep -Fq 'Apache License' LICENSE 2>/dev/null && grep -Fq 'Version 2.0' LICENSE 2>/dev/null; then
  pass 'Apache-2.0 LICENSE present'
else
  block 'LICENSE is missing or is not Apache License 2.0'
fi

if command -v dotnet >/dev/null 2>&1; then
  license_expr=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageLicenseExpression 2>/dev/null || true)
  license_file=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageLicenseFile 2>/dev/null || true)
  repo_url=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:RepositoryUrl 2>/dev/null || true)
  if [ "$license_expr" = 'Apache-2.0' ]; then pass 'NuGet license metadata is Apache-2.0'; else block "unexpected NuGet license metadata: ${license_expr:-<empty>}"; fi
  if [ "$repo_url" = 'https://github.com/charle-z/paytness' ]; then pass 'NuGet RepositoryUrl matches owner-bound repository'; else block "unexpected RepositoryUrl: ${repo_url:-<empty>}"; fi
else
  block '.NET SDK unavailable: cannot inspect package metadata'
fi

if grep -Fq 'org.opencontainers.image.licenses="Apache-2.0"' Dockerfile 2>/dev/null; then
  pass 'OCI license metadata is Apache-2.0'
else
  block 'OCI license metadata is not Apache-2.0'
fi

if grep -Fq 'NPL 4.0' reference/nopcommerce/LICENSING.md 2>/dev/null && grep -Fq 'does **not** publish a prebuilt nopCommerce-derived image' reference/nopcommerce/LICENSING.md 2>/dev/null; then
  pass 'nopCommerce NPL 4.0 reference boundary documented'
else
  block 'nopCommerce reference licensing boundary is incomplete'
fi

if grep -Fq 'GitHub Private Vulnerability Reporting' SECURITY.md 2>/dev/null; then
  pass 'SECURITY.md defines the private disclosure launch path'
else
  block 'SECURITY.md does not define GitHub Private Vulnerability Reporting'
fi

if [ -d .github/workflows ] && find .github/workflows -type f -print -quit 2>/dev/null | grep -q .; then
  info 'active GitHub workflows are present; verify Actions budget posture intentionally'
else
  pass 'repository workflows remain inactive in pre-alpha'
fi

if [ -z "$(git status --porcelain 2>/dev/null)" ]; then
  pass 'Git working tree is clean'
else
  info 'Git working tree is dirty (expected while preparing a release change, but release candidate must be clean)'
fi

if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
  alpha 'Docker Compose available for the first-alpha real-SUT gate'
else
  alpha 'Docker Compose unavailable here; first packaged alpha still requires the full nopCommerce gate on a normal Docker host'
fi

if command -v docker >/dev/null 2>&1 && docker buildx version >/dev/null 2>&1; then
  alpha 'Docker Buildx available for the first-alpha multiarch OCI gate'
else
  alpha 'Docker Buildx unavailable here; first packaged alpha still requires amd64/arm64 OCI + Trivy on a normal release host'
fi

alpha 'Smoke native release binaries before advertising their first packaged alpha surfaces'
alpha 'Have someone other than the author attempt the clean Quick Start before the first packaged alpha'
alpha 'Re-check then-current nopCommerce NPL 4.0 terms before redistributing any combined/derived reference artifact'
info 'Phase A external visibility/name/PVR checks are recorded in docs/PUBLICATION-CHECKLIST.md; re-check them only if repository settings or project identity change'

printf '\nAutomated source-preview blockers: %s\n' "$blocks"
if [ "$blocks" -ne 0 ]; then
  exit 1
fi
printf 'Repository-local source-preview preflight is clear; recorded Phase A external checks are complete and ALPHA release gates still apply.\n'
