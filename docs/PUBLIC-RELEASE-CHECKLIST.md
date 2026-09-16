# Public release checklist

The existing private repository should become public only after this checklist is satisfied; do not create a replacement repository solely for publication.

- [ ] local one-command verification passes on Linux and Windows;
- [ ] README quick start has been reproduced from a clean machine/workspace;
- [ ] license selected and `LICENSE` added;
- [ ] `SECURITY.md` contains a durable private disclosure channel;
- [ ] `CONTRIBUTING.md` matches the actual verification workflow;
- [ ] no secrets, credentials, production data, or private endpoints in tracked files/history;
- [ ] public ScenarioSpec/ProviderContract/JSON report versions documented;
- [ ] negative idempotency mutation still produces FAIL;
- [ ] bootstrap and teardown for the reference demo are automated;
- [ ] first alpha release artifacts are reproducible and checksumed;
- [ ] GitHub CI policy is intentionally enabled with cost/storage limits understood.

Target opening point: a reproducible `v0.1.0-alpha`, not necessarily the completion of every v0.1 feature.
