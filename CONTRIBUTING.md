# Contributing to Paytness

Paytness is intentionally small and evidence-driven. Prefer a narrow vertical change with tests over a new abstraction layer.

## One-command verification

Linux/macOS/WSL:

```sh
./eng/verify.sh
```

Windows PowerShell:

```powershell
./eng/verify.ps1
```

These commands are the local source of truth for restore, formatting, Release build, tests, scenario validation, and dependency vulnerability auditing. Future CI must call the same verification flow rather than reimplementing it differently.

`eng/verify.*` also runs the deterministic B1/B2/B6 quality-gate executable. Timing/RSS/B7 checks are intentionally separate so ordinary PR validation stays stable:

```sh
./eng/performance-gates.sh
```

Run the performance gate for changes to parsing, scheduling, execution/concurrency, provider hosting, evidence retention, or before a release candidate.

## Design constraints

- C# 14 / .NET 10; one executable and one process for the runner.
- No database in the Paytness core.
- ScenarioSpec and ProviderContract remain declarative and versioned.
- No arbitrary scripts, eval, shell, DLL plugins, insecure TLS flags, or implicit retries.
- Paytness never reads the SUT database.
- New dependencies require a concrete need and security/maintenance justification.

See `docs/PRD.md`, `docs/adr/`, `docs/QUALITY-GATES.md`, and `docs/BACKLOG.md` before changing public contracts.

## Real-SUT reference gate

Changes that affect provider semantics, idempotency, webhook contracts, scheduling/concurrency behavior, observation/invariants, or `reference/nopcommerce/` must also run the real reference gate before release/publication:

```sh
./reference/nopcommerce/run.sh
```

This is intentionally separate from `eng/verify.*` because it builds containers and recreates PostgreSQL several times. It must keep NOP-01..07 green and must prove that both required mutations are detected. Do not replace this gate with direct database assertions or manual admin-panel steps.
