# Paytness

Paytness is a local/CI adversarial test runner for REST + webhook payment integrations. Its core question is simple: **does one logical payment intent still produce exactly one economic effect and converge to the correct state when transport becomes unreliable?**

Paytness separates provider-side semantic commit from HTTP delivery, so it can reproduce failures that a normal happy-path sandbox usually misses.

## Quick start — real reference SUT

From a source checkout, the fastest useful demo requires **Docker with the Compose plugin**. You do not need a local .NET SDK, PostgreSQL installation, nopCommerce installation, browser setup, or manual plugin configuration.

```bash
./reference/nopcommerce/run.sh
```

That command builds the local test-only reference images, creates PostgreSQL, bootstraps nopCommerce 4.90.8, runs NOP-01..07, verifies both required negative mutations, writes JSON/JUnit evidence under `.artifacts/nopcommerce/`, and tears the stack down automatically.

A successful run ends with:

```text
Paytness nopCommerce reference passed: NOP-01..07 green and both required mutations detected.
```

The reference is built locally from the official upstream nopCommerce image and is not a prebuilt Paytness distribution artifact. It proves the workflow against a real external SUT; it is not a claim of compatibility with a production PSP.

## What Paytness tests

- provider commit followed by a lost HTTP response and an idempotent retry;
- duplicate and out-of-order HMAC-SHA256 webhooks;
- concurrent requests for one logical payment;
- eventual state convergence;
- explicit economic invariants such as request/attempt/effect counts;
- declarative YAML/JSON ScenarioSpec + ProviderContract, including nested response/webhook shapes through closed `$bind` values;
- bounded targets, parsing, scheduling, evidence retention and cancellation;
- console, JSON and JUnit evidence outputs.

Webhook deliveries are declarative and are never retried implicitly by Paytness. The core never reads the SUT database.

## CLI

```text
paytness validate <scenario>
paytness run <scenario> --sut <scheme://host:port>
paytness --version
```

Exit codes: `0` PASS, `1` invariant FAIL, `2` invalid spec/config, `3` infrastructure/security-policy error, `130` cancelled.

## Bring your own SUT

A test or staging integration must be able to point its provider traffic at Paytness and expose small test-only HTTP surfaces for driving the SUT, receiving webhooks and observing application state. Provider shape is configured declaratively; Paytness does not require direct database access or nopCommerce-specific code.

See `docs/PRD.md`, `docs/adr/002-scenario-spec.md` and `docs/adr/003-security-target-model.md` for the public contracts and trust boundaries.

## Verify locally

The canonical fast gate is:

```sh
./eng/verify.sh
```

Windows PowerShell:

```powershell
./eng/verify.ps1
```

It performs locked restore, formatting verification, Release build, the test suite, deterministic quality gates, ScenarioSpec validation, security smoke tests and dependency vulnerability auditing.

Resource/performance gates are intentionally separate:

```sh
./eng/performance-gates.sh
```

Changes that affect payment semantics or the nopCommerce integration must also pass the real reference gate before release:

```sh
./reference/nopcommerce/run.sh
```

GitHub Actions remains disabled during the private pre-alpha phase. Future CI must call these repository scripts instead of duplicating their logic.

## Distribution — not published yet

Build the five self-contained RIDs plus the local NuGet tool and checksums:

```sh
./eng/package.sh
```

Prepare the Linux amd64/arm64 OCI artifact on a normal Docker Buildx host:

```sh
./eng/package-oci.sh
```

Check release readiness without running the expensive release suite:

```sh
./eng/publication-status.sh
```

See `docs/DISTRIBUTION.md` and `docs/PUBLICATION-CHECKLIST.md` for the exact release surfaces and remaining external/manual gates.

## Documentation map

- `docs/PRD.md` — problem, product promise, invariants and v0.1 boundaries.
- `docs/adr/001-architecture.md` — runtime/process architecture.
- `docs/adr/002-scenario-spec.md` — ScenarioSpec and ProviderContract design.
- `docs/adr/003-security-target-model.md` — target/network/security model.
- `docs/QUALITY-GATES.md` — executable quality and release gates.
- `docs/DISTRIBUTION.md` — binaries, NuGet, OCI and GitHub Action packaging.
- `docs/nopcommerce-reference.md` — real-SUT reference architecture and evidence.
- `docs/PUBLICATION-CHECKLIST.md` — single source of truth for public-release readiness.
- `LICENSING.md` / `THIRD_PARTY_NOTICES.md` — licensing scope and third-party boundaries.

## Explicit non-goals for v0.1

No UI/SaaS, runner database, real PSP adapters, ISO 8583/20022 core, capture/refund/void/3DS, browser automation, arbitrary scripts/eval/plugins, Native AOT, broker, distributed runner, or packet-level chaos.

## License

Paytness core and normal distribution surfaces are licensed under the Apache License 2.0. See `LICENSE` and `LICENSING.md`.

The test-only nopCommerce 4.90.8 reference has a separate third-party boundary: Paytness-authored files remain Apache-2.0, while nopCommerce remains upstream NPL 4.0. Paytness does not publish a prebuilt nopCommerce-derived image by default. See `reference/nopcommerce/LICENSING.md` and `THIRD_PARTY_NOTICES.md`.
