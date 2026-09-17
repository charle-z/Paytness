# Paytness

Paytness is a local/CI adversarial test runner for REST + webhook payment integrations. Its core question is simple: **does one logical payment intent still produce exactly one economic effect and converge to the correct state when transport becomes unreliable?**

## v0.1 wedge

Paytness models provider-side semantic commit separately from HTTP delivery. It can reproduce response loss after commit, retries, duplicate and out-of-order webhooks, concurrency, and eventual convergence, then evaluate explicit invariants against provider state and test-only HTTP observations exposed by the system under test (SUT).

Paytness is not a generic mock server, a PSP emulator, a PCI certification tool, or a load-testing product.

## Quick start — real reference SUT

From a source checkout, the fastest useful demo requires **Docker with the Compose plugin**. You do not need a local .NET SDK, PostgreSQL installation, nopCommerce installation, browser setup, or manual plugin configuration.

```bash
./reference/nopcommerce/run.sh
```

That one command uses short-lived SDK containers to compile Paytness and the test-only reference plugin, assembles runtime-only local images, creates PostgreSQL, bootstraps nopCommerce, runs NOP-01..07, verifies both required negative mutations, writes JSON/JUnit evidence under `.artifacts/nopcommerce/`, and tears the stack down automatically. A successful run ends with:

```text
Paytness nopCommerce reference passed: NOP-01..07 green and both required mutations detected.
```

The nopCommerce reference is built locally from the official upstream image and is not a prebuilt Paytness distribution artifact. It proves the workflow against a real external SUT; it is not a claim of compatibility with a production PSP. To test your own SUT, expose test-only HTTP driver/observer endpoints and use the CLI contract below.

## Runtime and shape

- C# 14 / .NET 10 LTS (`net10.0`)
- one executable, one process
- ASP.NET Core Minimal APIs + embedded Kestrel
- in-memory state per run
- declarative YAML/JSON ScenarioSpec + ProviderContract
- console, JSON and JUnit evidence outputs

The core never reads the SUT database. Reference integrations expose test-only driver, webhook and observer HTTP endpoints.

## CLI target

```text
paytness validate <scenario>
paytness run <scenario> --sut <scheme://host:port>
paytness --version
```

Exit codes: `0` PASS, `1` invariant FAIL, `2` invalid spec/config, `3` infrastructure/security-policy error, `130` cancelled.

## Development order

1. Healthy external payment.
2. Provider commits, response is lost, SUT retries, one economic effect remains.
3. Duplicate webhook + HMAC + JUnit.
4. Deterministic scheduler + out-of-order delivery.
5. Concurrency.
6. Full security boundary.
7. nopCommerce 4.90.8 reference + required mutations.
8. Distribution.

See `docs/PRD.md`, `docs/adr/`, `docs/BACKLOG.md`, `docs/QUALITY-GATES.md`, and `docs/nopcommerce-reference.md`.

## Explicit non-goals for v0.1

No UI/SaaS, runner database, real PSP adapters, ISO 8583/20022 core, capture/refund/void/3DS, browser automation, arbitrary scripts/eval/plugins, Native AOT, broker, distributed runner, or packet-level chaos.

## Developer verification

The repository has one canonical local verification flow. It does not require repository-specific global configuration.

Linux/macOS/WSL:

```sh
./eng/verify.sh
```

Windows PowerShell:

```powershell
./eng/verify.ps1
```

The command performs locked restore, format verification, Release build, the full test suite, ScenarioSpec contract smoke tests, and dependency vulnerability auditing. Future CI must invoke the same flow rather than duplicate a second implementation of the gates.

## Implemented so far

Slices 1–8 are implemented in the current pre-publication tree:

- healthy external payment;
- semantic provider commit followed by a lost response and idempotent retry;
- explicit duplicate and out-of-order webhooks with HMAC-SHA256;
- bounded deterministic scheduling and bounded concurrent SUT actions;
- JSON/JUnit evidence with explicit PASS/FAIL exit codes;
- hardened target, parser, path, output, response and evidence boundaries;
- a real nopCommerce 4.90.8 + PostgreSQL 16 reference SUT;
- NOP-01..07 end-to-end reference scenarios;
- required `unstable-idempotency` and `accept-stale-state` mutation gates.

Webhook deliveries are declarative and are never retried implicitly by Paytness.

## Development: one-command verification

The fast local gate is intentionally automated and is the source of truth that future CI must call:

```bash
./eng/verify.sh
```

```powershell
./eng/verify.ps1
```

It performs locked restore, formatting verification, Release build, tests, ScenarioSpec contract smoke tests, adversarial security smoke tests, and dependency vulnerability audit. It keeps .NET CLI state under ignored local directories so running the gate does not require editing project files or configuring repository-specific global tooling.

## Real nopCommerce reference gate

The slower real-SUT gate is separate from the fast developer loop. It requires Docker with the Compose plugin. On Windows, run it from WSL:

```bash
./reference/nopcommerce/run.sh
```

That single command is designed to build the reference images, create PostgreSQL, bootstrap nopCommerce without browser/admin-panel steps, run NOP-01..07, verify both required mutations, write JSON/JUnit artifacts under `.artifacts/nopcommerce/`, and tear everything down.

See `docs/nopcommerce-reference.md` for the exact contracts, verified results, security posture, and the nested-Devbox Docker build limitation.
Third-party licensing boundaries for the reference are recorded in `THIRD_PARTY_NOTICES.md`; the root Paytness license decision remains separate.

GitHub Actions is intentionally not enabled during the private pre-alpha phase; when enabled, workflows must reuse these gates rather than maintain divergent build/test logic.

## Distribution (pre-publication)

Release surfaces are implemented but intentionally **not published yet**.

Build and smoke the five self-contained single-file RIDs plus the local NuGet tool and checksums:

```bash
./eng/package.sh
```

Prepare the Linux amd64/arm64 OCI artifact on a normal Docker Buildx host:

```bash
./eng/package-oci.sh
```

The repository also contains a thin composite `action.yml`. It installs the `Paytness` NuGet tool and invokes the CLI; it does not duplicate Paytness logic. The Action has been tested locally against a real HTTP SUT using a locally packed NuGet source, but it cannot be consumed from a public tag until both the repository and NuGet package are actually published.

Repository GitHub workflows remain deliberately disabled in pre-alpha. Reviewed templates live under `eng/ci/`. Enabling them requires an explicit local opt-in:

```bash
PAYTNESS_ENABLE_GITHUB_ACTIONS=1 ./eng/enable-github-workflows.sh
```

Do not enable/push them merely to make CI look complete: see `docs/PUBLICATION-CHECKLIST.md` first.
