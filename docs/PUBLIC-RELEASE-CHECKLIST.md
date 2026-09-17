# Public release checklist

The existing private repository should become public only after this checklist is satisfied; do not create a replacement repository solely for publication.

- [ ] local one-command verification passes on Linux and Windows;
- [ ] controlled performance/B7 gate passes and measurements are recorded;
- [ ] README quick start has been reproduced from a clean machine/workspace;
- [ ] Paytness core license selected and `LICENSE` added;
- [ ] `reference/nopcommerce/` NPL 4.0 license/notice posture explicitly resolved;
- [ ] `SECURITY.md` contains a durable private disclosure channel;
- [ ] `CONTRIBUTING.md` matches the actual verification workflow;
- [ ] no secrets, credentials, production data, or private endpoints in tracked files/history;
- [ ] public ScenarioSpec/ProviderContract/JSON report versions documented;
- [ ] negative idempotency mutation still produces FAIL;
- [ ] bootstrap, local image build and teardown for the reference demo are automated without a local .NET SDK;
- [ ] no prebuilt nopCommerce-derived reference image is published by default;
- [ ] first alpha release artifacts are reproducible and checksumed;
- [ ] GitHub CI policy is intentionally enabled with cost/storage limits understood.

Target opening point: a reproducible `v0.1.0-alpha`, not necessarily the completion of every v0.1 feature.
