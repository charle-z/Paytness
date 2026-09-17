# Working on Paytness

Keep changes small, evidence-driven, and inside the v0.1 product boundary. Do not add abstractions or dependencies without a concrete need.

## Repository map

- `src/Paytness/` — product code.
- `tests/` — unit, integration, quality and performance gates.
- `scenarios/` + `contracts/` — public declarative ScenarioSpec/ProviderContract examples.
- `reference/nopcommerce/` — test-only real SUT reference; it is not Paytness core.
- `eng/` — canonical verification, packaging and release scripts.
- `docs/PRD.md` — product boundary and invariants.
- `docs/QUALITY-GATES.md` — required evidence.

## Technical baseline

- C# 14 / .NET 10, pinned by `global.json`.
- One runner process with embedded Kestrel; core state is in-memory per run.
- ScenarioSpec and ProviderContract stay declarative and versioned.
- No arbitrary scripts/eval/shell/DLL plugins in contracts.
- No database access from Paytness core and no nopCommerce concepts in core.
- Webhook HMAC is verified over exact request bytes.
- Paytness does not add implicit webhook retries.
- Preserve bounded parsing, scheduling, concurrency, evidence and network-target controls.

## Product invariant

The core question is whether one logical payment intent still produces exactly one economic effect and converges correctly under unreliable transport. Changes must not hide duplicate effects, stale-state regressions or ambiguous commits.

## Verify changes

Default local gate:

```sh
./eng/verify.sh
```

Also run:

- `./eng/performance-gates.sh` for parsing, scheduling, concurrency, hosting or evidence-retention changes.
- `./reference/nopcommerce/run.sh` for payment semantics or reference-integration changes, on a normal Docker + Compose host.
- `./eng/package-repro-check.sh` for packaging/distribution changes.
- `./eng/publication-status.sh` for release/legal/documentation changes.

Do not duplicate these command lists in new CI logic; call the repository scripts.

## Release boundaries

- Do not publish NuGet, OCI images, releases, change repository visibility, deploy, or enable GitHub workflows unless explicitly authorized.
- Paytness core is Apache-2.0. Respect `LICENSING.md` and the separate nopCommerce NPL 4.0 boundary.
- Do not publish a prebuilt nopCommerce-derived reference image by default.
- Do not claim PSP compatibility, PCI compliance, production adoption or performance beyond recorded evidence.

## Git

Use focused conventional commits. Preserve repository hooks and existing attribution. Never discard unrelated work to make a change fit.
