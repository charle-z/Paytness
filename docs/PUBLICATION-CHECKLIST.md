# Paytness publication checklist

Public source visibility and a packaged release are separate milestones. A public **pre-alpha source preview** may happen before Paytness has native release smokes, published packages, or external users. Publishing `v0.1.0-alpha.1`, NuGet, OCI or GitHub release assets remains a stricter gate.

Use `./eng/publication-status.sh` (or `.ps1`) for the cheap repository-local **source-preview** preflight. `./eng/publication-audit.sh` remains the expensive first-alpha/release audit.

## Phase A — public source preview

Required before changing repository visibility:

- [x] Technical name screen completed 2026-09-17: exact `paytness` entry absent from NuGet, PyPI and npm registry APIs; GitHub repository search returned no exact-name repository. Re-check package availability immediately before first package publish.
- [x] Manual WIPO and Colombia SIC name searches were completed before visibility change with no conflicting `Paytness` registration identified; this is a practical pre-alpha screen, not legal trademark clearance.
- [x] Paytness core is Apache-2.0; canonical text is in `LICENSE` and NuGet/OCI metadata matches.
- [x] nopCommerce 4.90.8 / NPL 4.0 is an explicit test-only reference boundary; no prebuilt nopCommerce-derived image is published by default.
- [x] Generated `.artifacts/`, `dist/`, `.agent-memory/`, nopCommerce DLL refs and local reference artifacts are ignored.
- [x] The tracked publication candidate tree has a Gitleaks gate and publication hygiene checks.
- [x] Full Git history was reviewed with Gitleaks before first visibility change; no leaked secrets were detected in the reviewed 22-commit history.
- [x] Historical generated/private-state paths (`.artifacts/`, `.agent-memory/`, `.local/`, `dist/`, nopCommerce `.refs`) were never tracked in the reviewed history.
- [x] `SECURITY.md` defines GitHub Private Vulnerability Reporting as the public disclosure channel.
- [x] The reference plugin/Compose configuration is clearly test-only and must not be treated as production deployment guidance.
- [x] `./eng/publication-status.sh` passed on the exact source-preview commit before visibility change.
- [x] Repository visibility is public and GitHub Private Vulnerability Reporting is enabled and verified.

A source preview does **not** require a published NuGet package/image, a GitHub release, active Actions workflows, native Windows/macOS release smokes, normal-host Buildx/Compose release gates, or an external first-run tester.

**Phase A status: complete.** Paytness is currently a public pre-alpha source preview.

## Phase B — first packaged alpha

Required before `v0.1.0-alpha.1` (or equivalent), NuGet, OCI/GHCR or GitHub release assets:

- [ ] Choose the first public prerelease version/tag and keep package/binary/image versions aligned.
- [ ] `./eng/verify.sh` PASS.
- [ ] `./eng/performance-gates.sh` PASS in a controlled Linux x64 Release environment and record the measurements.
- [ ] `./reference/nopcommerce/run.sh` PASS on a normal Docker + Compose host from a fresh reference stack.
- [ ] NOP-01..07 PASS and both required mutations fail for the documented reasons.
- [ ] `./eng/package-repro-check.sh` PASS twice on the exact release commit.
- [ ] Smoke Linux x64 and Windows x64 release binaries on their native OSes; smoke macOS before advertising macOS as a supported release surface.
- [ ] `./eng/package-oci.sh` PASS with Linux amd64+arm64 on normal Docker Buildx.
- [ ] Pinned Trivy HIGH/CRITICAL scan PASS for both release image platforms.
- [ ] Install the release-candidate `.nupkg` from an isolated source and run `--version` + a ScenarioSpec.
- [ ] Have at least one person other than the author attempt the README Quick Start from a clean environment; record where they block and whether they reach a useful PASS/FAIL without private context.
- [ ] Re-check NuGet PackageId `Paytness` immediately before first publish.
- [ ] Re-check then-current nopCommerce NPL 4.0 terms before redistributing any combined/derived reference artifact; local-only reference builds remain the default.
- [ ] Create checksummed GitHub release artifacts only from the exact gated commit.
- [ ] Publish NuGet/OCI only after the matching release gates pass; only then point examples at real published identifiers.

## GitHub Actions posture

- Do not enable workflows merely because templates/action metadata exist.
- Once the repository is public, enable the fast PR/main gate only if its maintenance/compute cost is justified.
- Keep the expensive nopCommerce reference gate manual/release-triggered initially.
- Avoid scheduled workflows until there is evidence they provide value.
- Reuse repository scripts; do not duplicate build/test matrices in workflow YAML.

## Product claims

- Do not claim PCI compliance, PSP compatibility, production volume, money saved, user adoption or incident prevention without evidence.
- Clearly label the nopCommerce plugin/Compose configuration as test-only.
- Keep v0.1 limits explicit: REST JSON + webhooks, no real PSP adapters, no SaaS/UI and no packet-level chaos.
