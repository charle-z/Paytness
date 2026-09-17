#!/usr/bin/env sh
set -u

ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT" || { echo "Cannot enter repository root: $ROOT" >&2; exit 2; }

blocks=0
pass() { printf '[PASS] %s\n' "$*"; }
block() { printf '[BLOCK] %s\n' "$*"; blocks=$((blocks + 1)); }
info() { printf '[INFO] %s\n' "$*"; }
external() { printf '[EXTERNAL] %s\n' "$*"; }
manual() { printf '[MANUAL] %s\n' "$*"; }

printf 'Paytness publication preflight\n\n'

for required in README.md SECURITY.md CONTRIBUTING.md THIRD_PARTY_NOTICES.md docs/PUBLICATION-CHECKLIST.md docs/DISTRIBUTION.md; do
  if [ -f "$required" ]; then pass "$required present"; else block "$required missing"; fi
done

if [ -f LICENSE ]; then
  pass 'LICENSE present'
else
  block 'LICENSE missing: choose the Paytness core license before publication'
fi

if command -v dotnet >/dev/null 2>&1; then
  license_expr=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageLicenseExpression 2>/dev/null || true)
  license_file=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:PackageLicenseFile 2>/dev/null || true)
  repo_url=$(dotnet msbuild src/Paytness/Paytness.csproj -nologo -getProperty:RepositoryUrl 2>/dev/null || true)
  if [ -n "$license_expr" ] || [ -n "$license_file" ]; then pass 'NuGet license metadata present'; else block 'NuGet license metadata missing'; fi
  if [ "$repo_url" = 'https://github.com/charle-z/paytness' ]; then pass 'NuGet RepositoryUrl matches owner-bound repository'; else block "unexpected RepositoryUrl: ${repo_url:-<empty>}"; fi
else
  block '.NET SDK unavailable: cannot inspect package metadata'
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
  pass 'Docker Compose available for real-SUT gate'
else
  external 'Docker Compose unavailable here: run the full nopCommerce gate on a normal Docker host'
fi

if command -v docker >/dev/null 2>&1 && docker buildx version >/dev/null 2>&1; then
  pass 'Docker Buildx available for multiarch OCI gate'
else
  external 'Docker Buildx unavailable here: run amd64/arm64 OCI + Trivy on a normal release host'
fi

external 'Smoke Windows x64 and macOS release binaries on their native OSes'
external 'Have someone other than the author reproduce the clean Quick Start / first useful PASS/FAIL'
manual 'Resolve the explicit NPL 4.0 license/notice treatment for reference/nopcommerce/'
manual 'Perform final trademark/name review in the official dynamic trademark databases'
manual 'After repository visibility changes, enable GitHub Private Vulnerability Reporting and verify the Report a vulnerability button before announcing a release'

printf '\nAutomated repository blockers: %s\n' "$blocks"
if [ "$blocks" -ne 0 ]; then
  exit 1
fi
printf 'Repository-local preflight is clear; external/manual publication gates still apply.\n'
