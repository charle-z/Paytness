# Paytness v0.1 — Quality Gates

## Pull requests

Required:

- reproducible restore / lock file when dependencies are introduced;
- Release build;
- formatting verification;
- warnings as errors (`TreatWarningsAsErrors=true`, `AnalysisLevel=10-recommended`);
- unit tests;
- property tests for high-value invariants;
- contract tests for ScenarioSpec/ProviderContract/report schemas;
- synthetic integration tests using real Kestrel;
- fast security matrix;
- dependency vulnerability audit.

ProviderContract expressiveness is also gated by both the canonical contract and `scenarios/alternate-provider-shape.yaml`, which uses different endpoints, idempotency header, nested request extraction, nested response skeleton, nested webhook skeleton and signature header without scripts or custom runner code. This is an internal compatibility proof, not an external-user adoption claim.

Coverage is informational; there is no global percentage gate. Direct tests are mandatory for state machines, idempotency, scheduler ordering, target policy, redaction and ScenarioSpec validation. `eng/verify.*` also executes `Paytness.QualityGates`, which exercises deterministic B1/B2/B6 boundaries without relying on timing-sensitive unit-test assertions.

## Functional benchmarks

B1 parser limits and rejection boundaries.
B2 2,000 scheduled actions with stable ordering.
B3 100 concurrent same-key requests => 100 requests, 1 attempt, 1 effect.
B4 100 distinct-key requests for one logical payment expose broken idempotency rather than hiding it.
B5 EvidenceStore pressure and 64 MiB cap/truncation.
B6 cancellation with active webhooks/polls; no false PASS; shutdown <= 5 s.
B7 repeated healthy runs do not show linear retained-memory growth.

Baseline targets on Linux x64 Release are engineering gates, not commercial SLAs: provider-ready p95 <=1.5 s; normal validation p95 <=500 ms; 1 MiB validation p95 <=1 s; small-runner overhead <=500 ms excluding SUT; RSS <200 MiB under normal maximum benchmark; scheduler lag p95 <25 ms on non-saturated CPU.

## Controlled performance gate

Performance and retained-memory checks are intentionally separate from the fast PR loop:

```sh
./eng/performance-gates.sh
```

```powershell
./eng/performance-gates.ps1
```

The gate measures the documented Linux x64 engineering budgets and B7 over 100 healthy runs. B7 also uses deliberately loose regression guards: managed-memory delta must remain <=5 MiB across the sampled run window and fitted managed-memory slope must remain <=64 KiB/run. These are regression tripwires, not commercial SLAs. The observed Devbox baseline was about +0.02 MiB total and ~259 bytes/run.

## Main branch / reference

- nopCommerce NOP-01..07 green.
- `unstable-idempotency` mutation must fail the one-effect invariant.
- `accept-stale-state` mutation must make the out-of-order scenario fail.

## Release

Smoke published RIDs, OCI multiarch, dotnet tool, checksums and image vulnerability scan. No claim of PCI compliance, real gateway compatibility, users, savings or production volume without evidence.

## Canonical local gate

`eng/verify.sh` and `eng/verify.ps1` are the source of truth for local validation. Any future GitHub workflow must call the same verification flow instead of maintaining a divergent list of build/test commands.

GitHub Actions is intentionally not enabled automatically while the repository remains private and pre-alpha. Enabling scheduled/push/PR workflows is a deliberate publication decision so storage and compute usage remain controlled.

## Real-SUT reference gate

The nopCommerce reference gate is intentionally slower than the canonical fast gate and is mandatory for changes that can alter payment reliability semantics or the reference integration:

```sh
./reference/nopcommerce/run.sh
```

Required result:

- NOP-01..07 PASS against nopCommerce 4.90.8 + PostgreSQL 16;
- `unstable-idempotency` makes NOP-03 exit 1 and exposes two economic effects;
- `accept-stale-state` makes NOP-05 exit 1 with final `Pending`;
- JSON and JUnit artifacts are produced automatically;
- the database/stack is recreated for mutation gates so previous runs cannot mask defects.

The fast `eng/verify.*` gate must remain usable without Docker. CI should keep these as separate jobs rather than making every edit pay the real-SUT startup cost.

## Reproducible release artifacts

`./eng/package-repro-check.sh` must produce identical SHA-256 manifests across two complete packaging runs of the same Git commit. The build epoch is derived from the commit timestamp; archives normalize order, timestamps and ownership, and NuGet uses deterministic package timestamps.
