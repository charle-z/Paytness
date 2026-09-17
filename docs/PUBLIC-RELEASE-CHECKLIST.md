# Public release checklist

The existing private repository should become public only after this checklist is satisfied; do not create a replacement repository solely for publication.

- [ ] local one-command verification passes on Linux and Windows;
- [ ] controlled performance/B7 gate passes and measurements are recorded;
- [ ] README quick start has been reproduced from a clean machine/workspace;
- [x] Paytness core license selected as Apache-2.0 and `LICENSE` added;
- [x] `reference/nopcommerce/` NPL 4.0 boundary explicitly documented; combined/derived reference artifacts are not treated as Apache-2.0-only;
- [ ] `SECURITY.md` names GitHub Private Vulnerability Reporting as the launch channel; after visibility changes, enable it and verify `Report a vulnerability` before publishing/announcing a release.
- [ ] `CONTRIBUTING.md` matches the actual verification workflow;
- [ ] no secrets, credentials, production data, or private endpoints in tracked files/history;
- [ ] public ScenarioSpec/ProviderContract/JSON report versions documented;
- [ ] negative idempotency mutation still produces FAIL;
- [ ] bootstrap, local image build and teardown for the reference demo are automated without a local .NET SDK;
- [x] no prebuilt nopCommerce-derived reference image is published by default;
- [ ] first alpha release artifacts are reproducible and checksumed;
- [ ] GitHub CI policy is intentionally enabled with cost/storage limits understood.

Target opening point: a reproducible `v0.1.0-alpha`, not necessarily the completion of every v0.1 feature.
