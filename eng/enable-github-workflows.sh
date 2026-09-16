#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

if [ "${PAYTNESS_ENABLE_GITHUB_ACTIONS:-}" != "1" ]; then
  echo "Refusing to enable GitHub workflows without PAYTNESS_ENABLE_GITHUB_ACTIONS=1." >&2
  echo "Enabling these files means future pushes/PRs can consume GitHub Actions minutes." >&2
  exit 2
fi

mkdir -p .github/workflows
cp eng/ci/github-fast.yml .github/workflows/verify.yml
cp eng/ci/github-reference.yml .github/workflows/reference.yml
printf '%s\n' "GitHub workflows enabled locally. Review the diff before any push."
