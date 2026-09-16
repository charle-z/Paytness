# Paytness v0.1 — Backlog

Work is organized as vertical slices that each produce observable value.

## Slice 1 — Healthy external payment

- CLI: `validate`, `run`, `--version` and exit codes.
- YAML/JSON ScenarioSpec v1 parsing with initial bounds.
- ProviderContract v1 parsing and validation.
- In-memory provider on Kestrel.
- One SUT action and one HTTP observation.
- Provider request/attempt/effect accounting.
- Typed invariant evaluation.
- Console + JSON report.
- Unit/contract/integration tests proving `paytness run healthy.yaml` => PASS.

## Slice 2 — Ambiguous commit and retry

- idempotency registry + canonical payload fingerprint.
- semantic commit before `abort_after_commit` transport failure.
- same key + same payload reuses attempt/effect.
- same key + different payload => 409 without second effect.
- evidence timeline for request/attempt/response.
- positive stable-key retry and negative broken-idempotency mutation.

Gate: stable retry yields >=2 requests, 1 attempt, 1 effect, 1 key; unstable retry yields >=2 attempts/effects and `one-economic-effect` FAIL.

## Slice 3

Duplicate webhooks, HMAC-SHA256, JUnit output.

## Slice 4

Bounded deterministic scheduler, delayed and out-of-order webhook delivery.

## Slice 5

Concurrent requests and one-logical-payment contention behavior.

## Slice 6

Full target policy, DNS connect pinning, redirects/TLS policy, path/parser/log/secret hardening and resource caps.

## Slice 7

nopCommerce 4.90.8 + PostgreSQL 16 Docker Compose reference, seven scenarios and required mutations.

## Slice 8

Self-contained single-file RIDs, OCI multiarch, dotnet tool, checksums and thin GitHub Action.

## Frozen out of scope

UI/SaaS, runner database, real PSP adapters, ISO 8583/20022 core, capture/refund/void/3DS, browser automation, arbitrary expressions/scripts/DLL plugins, Native AOT, broker, distributed runner and packet-level chaos.

## Implementation checkpoint — 2026-09-16

Slices 1–7 are implemented. Slice 8 (distribution/publication) is the next vertical slice.

### Slices 1–3

The CLI, ScenarioSpec/ProviderContract, stateful provider, typed invariants, JSON/JUnit evidence, ambiguous commit/retry semantics, duplicate webhooks and exact-byte HMAC are executable. Stable retry proves one economic effect; the unstable-idempotency negative case proves Paytness detects two.

### Slice 4

The bounded `Channel<ScheduledAction>` + single-reader `PriorityQueue` scheduler has a 2,000-action hard cap and stable due-time/ordinal ordering. Declarative `delayMs` drives out-of-order delivery without one `Task.Delay` per webhook.

### Slice 5

Scenario actions support bounded `concurrency` (1–128), with a shared outbound `SemaphoreSlim(32)`. Same-key contention preserves one attempt/effect while distinct keys expose duplicate economic effects instead of hiding them.

### Slice 6

The security boundary is executable: target allowlisting plus connect-time DNS validation, disabled redirects/decompression, bounded HTTP/Kestrel/provider/evidence resources, redaction-before-retention, sanitized human/CI output, structural YAML/JSON limits, hostile YAML rejection, lexical + physical contract containment, and cancellation/truncation gates. `eng/security-smoke.sh` / `.ps1` are part of the canonical fast verification flow.

### Slice 7

The nopCommerce 4.90.8 reference is implemented against PostgreSQL 16. The test-only plugin uses normal nopCommerce services and the real `OrderPaidEvent`; no Paytness core code reads PostgreSQL or knows nopCommerce internals. Empty-database bootstrap uses nopCommerce migrations plus its own `IInstallationService`, so no browser/admin-panel setup is required.

Verified end-to-end on real nopCommerce/PostgreSQL processes:

- NOP-01 Healthy — PASS;
- NOP-02 commit then response lost — PASS while Order remains uncertain/Pending;
- NOP-03 stable retry — PASS with 2 requests, 1 attempt/effect/key;
- NOP-04 duplicate webhook — PASS with 2 deliveries, 1 application;
- NOP-05 out-of-order webhook — PASS and final Paid;
- NOP-06 eventual convergence — PASS after delayed succeeded webhook;
- NOP-07 concurrent same payment — PASS with 2 requests and 1 attempt/effect/key.

Required mutation gates are also verified from fresh databases:

- `unstable-idempotency` => `one-economic-effect` FAIL with actual=2;
- `accept-stale-state` => `paid` FAIL with actual=Pending.

A one-command Docker Compose harness is materialized at `reference/nopcommerce/run.sh`. The nested Devbox toolbox cannot execute Dockerfile `RUN` steps because its parent sandbox denies `setgroups`; this is a validation-environment constraint, not a product failure. Individual image ingredients and the complete reference behavior have been executed successfully in Devbox.

### Slice 8 — next

- publish/smoke self-contained single-file RIDs;
- OCI multiarch image and checksums;
- `dotnet tool` packaging;
- thin GitHub Action that delegates to canonical repository gates;
- release/public-repo hygiene, license and publication checklist.
