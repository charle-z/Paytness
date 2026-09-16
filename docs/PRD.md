# Paytness v0.1 — Product Requirements

## Problem

Payment integrations fail at the boundary between economic state and transport state. A provider may process a payment while the HTTP response is lost. A reasonable retry can then create a second effect if idempotency is wrong. Sandboxes that merely return expected HTTP codes do not prove that the SUT stays coherent.

## User

Initial users are backend/integration engineers, QA/SDETs and production-support engineers maintaining REST + webhook payment integrations that can point a test/staging environment at an alternate provider endpoint and expose small test-only HTTP driver/observer endpoints.

## Product promise

Given one logical payment intent, Paytness reproduces ambiguous transport, retries, duplicates, reordering and concurrency and returns reproducible evidence showing whether the provider and SUT produced exactly the intended effect and converged.

First useful PASS/FAIL should be reachable in under ten minutes from the README by a new user.

## Required v0.1 behavior

- stateful REST JSON fake provider
- semantic commit distinct from HTTP response delivery
- idempotency registry keyed by key + canonical payload fingerprint
- duplicate and out-of-order webhook delivery
- deterministic bounded scheduling where Paytness controls ordering
- SUT actions and observations over HTTP only
- explicit typed invariants
- bounded, sanitized evidence with stable IDs and seed/scenario hash
- console and JSON reports; JUnit by Slice 3

## Domain invariants

`merchantReference` identifies a `LogicalPayment` but does not deduplicate it. `ProviderRequest`, `ProviderAttempt`, `ProviderResponse`, `PaymentEvent`, `WebhookDelivery`, `ObservedApplicationState`, and `InvariantResult` are separate concepts.

Idempotency semantics:

- same key + same payload => same `ProviderAttempt`, no second effect;
- same key + different payload => HTTP 409 `idempotency_key_reused`, no second effect;
- different key + same logical payment => may create another attempt/effect;
- missing key => each accepted operation may create a new attempt/effect.

Paytness must never hide broken SUT idempotency.

## Success gates

The critical gate is negative: a controlled unstable-idempotency mutation must create two provider attempts/effects and make `one-economic-effect` FAIL. If that mutation passes, the product has not demonstrated its thesis.

The nopCommerce reference must integrate through public HTTP contracts and a test-only plugin without core changes or direct database reads by Paytness.

## Honest boundaries

Only synthetic payment data. No PAN/CVV/track/PIN. No claim of PCI compliance, real-gateway compatibility, production certification, or universal transport semantics.
