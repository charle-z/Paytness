# Paytness public-release checklist

This checklist is intentionally blocking. A green local build is not authorization to make the repository public or publish packages/images.

Use `./eng/publication-status.sh` for a cheap aggregate preflight; it reports all currently detectable repository-local blockers before the expensive full audit.

## Identity and legal

- [ ] Re-check `Paytness` name across relevant software/package/repository/trademark channels close to publication date.
- [x] Paytness core license selected as Apache-2.0; canonical text is in `LICENSE`.
- [x] nopCommerce reference boundary documented in `reference/nopcommerce/LICENSING.md`: Paytness-authored files are Apache-2.0, but combined/derived nopCommerce artifacts are not treated as Apache-2.0-only.
- [x] Default release automation does not publish a prebuilt nopCommerce-derived reference image.
- [x] `THIRD_PARTY_NOTICES.md` records nopCommerce 4.90.8 / NPL 4.0 and the separate reference boundary.
- [x] NuGet and OCI metadata declare `Apache-2.0`; normal release payloads carry the license text.
- [ ] Choose the first public prerelease version/tag (for example an alpha) and make package/binary/image versions match.

## Security and repository hygiene

- [ ] Review the entire to-be-published tree for secrets, internal paths, credentials and private infrastructure references.
- [ ] Review Git history before the first push/public visibility change; do not assume current working-tree cleanliness proves history safety.
- [ ] Re-check then-current nopCommerce NPL 4.0 terms before redistributing any combined/derived reference artifact; local-only reference builds remain the default.
- [ ] Confirm generated `.artifacts/`, `dist/`, nopCommerce DLL refs and local toolbox state are ignored.
- [ ] Re-run `SECURITY.md`/threat-boundary review.
- [ ] Confirm reference-only settings/plugin cannot be mistaken for production guidance.

## Required technical gates

- [ ] `./eng/verify.sh` PASS.
- [ ] `./eng/performance-gates.sh` PASS in a controlled Linux x64 Release environment and record the measurements.
- [ ] `./reference/nopcommerce/run.sh` PASS on a normal Docker + Compose host, not only the nested Devbox workaround.
- [ ] NOP-01..07 PASS from a fresh reference stack.
- [ ] `unstable-idempotency` and `accept-stale-state` mutation gates FAIL for the required reasons.
- [ ] `./eng/package-repro-check.sh` PASS: two consecutive builds of the same commit produce identical `SHA256SUMS`.
- [ ] Smoke at least Linux x64 and Windows x64 release binaries on their native OSes.
- [ ] `./eng/package-oci.sh` PASS with Linux amd64+arm64 OCI output on normal Docker Buildx.
- [ ] Run an image vulnerability scan with the release-candidate image and record the scanner/version/result.
- [ ] Install the release-candidate `.nupkg` from an isolated source and run `--version` + a ScenarioSpec.

## External distribution

- [ ] Re-check that NuGet PackageId `Paytness` is still available before first publish.
- [ ] Publish NuGet only after license/version metadata are final.
- [ ] Publish OCI/GHCR only after multiarch build and image scan gates pass.
- [ ] Create checksummed GitHub release artifacts from the exact gated build.
- [ ] Only then make/update examples to point at real published package/image/tag identifiers.

## GitHub Actions budget posture

- [ ] Do not enable active repository workflows merely because templates/action metadata exist.
- [ ] Enable the fast PR/main gate only when repository publication is intentional.
- [ ] Keep the expensive nopCommerce reference gate manual/release-triggered initially unless usage justifies more frequency.
- [ ] Avoid scheduled workflows while there is no evidence they provide value.
- [ ] Reuse repository scripts; do not duplicate build/test matrices in workflow YAML.

## Product claims

- [ ] Do not claim PCI compliance, PSP compatibility, production volume, money saved, user adoption or incident prevention without evidence.
- [ ] Clearly label the nopCommerce plugin/Compose configuration as test-only.
- [ ] Document v0.1 limits: REST JSON + webhooks, no real PSP adapters, no SaaS/UI, no packet-level chaos.

## Public transition

- [ ] README quick start has been executed by someone other than the author from a clean environment.
- [ ] First useful PASS/FAIL can be reached without private context or manual repository surgery.
- [ ] `CONTRIBUTING.md`, `SECURITY.md`, ADRs and release notes match actual behavior.
- [ ] Change repository visibility only after all blocking items above are resolved.
- [ ] Treat visibility change + enabling GitHub Private Vulnerability Reporting as one launch sequence; do not tag/publish/announce until the private reporting button is verified.
