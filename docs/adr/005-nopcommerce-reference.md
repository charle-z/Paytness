# ADR-005 — nopCommerce as the real reference SUT

**Status:** accepted and implemented on 2026-09-16.

## Context

Paytness needs a reproducible external application that proves its payment-reliability invariants are not artifacts of the synthetic SUT. The reference must remain external to Paytness core and must expose application state without giving the runner direct database access.

## Decision

Use nopCommerce 4.90.8 with PostgreSQL 16 and a test-only `Paytness.Reference` plugin.

The plugin:

- is enabled only by explicit reference mode;
- implements a real nopCommerce `IPaymentMethod`;
- drives payments through normal nopCommerce services;
- observes the real `Order` and real `OrderPaidEvent`;
- receives HMAC-verified webhooks and deduplicates by event id;
- exposes only `/test/pay`, `/test/webhook`, and `/test/state` for the reference harness;
- never requires Paytness core to understand nopCommerce or query PostgreSQL.

Bootstrap uses normal nopCommerce migrations and its own `IInstallationService`. PostgreSQL enables the `citext` extension before nopCommerce starts. No browser/admin-panel installation is part of the supported reference workflow.

The seven NOP scenarios and both required mutations are executed by an automated Compose harness. Image preparation uses short-lived SDK containers and runtime-only final images, so the public quick start requires Docker but not a locally installed .NET SDK.

## Consequences

Positive:

- proves real application/service/event behavior;
- keeps reference-specific code out of Paytness core;
- allows an empty database to become runnable without manual setup;
- mutation gates can break real SUT behavior instead of a mocked assertion.

Costs:

- the reference plugin targets net9.0 because nopCommerce 4.90.8 does;
- real-SUT startup is much slower than the synthetic gate;
- Docker is required for the one-command public reference gate;
- nopCommerce 4.90.8 uses NPL 4.0, so the reference subtree has a separate licensing/publication gate and no prebuilt nopCommerce-derived image is published by default.

## Rejection criteria

Revisit the reference if it requires changes to nopCommerce core, direct PostgreSQL reads from Paytness, browser automation for normal setup, nopCommerce-specific abstractions in Paytness core, or licensing obligations that cannot be cleanly isolated from Paytness core. If licensing forces a pivot, current evidence favors a minimal `dotnet/eShop`-based reference over SimplCommerce.
