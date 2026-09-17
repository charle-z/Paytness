# ADR-002 — ScenarioSpec and ProviderContract v1

Status: Accepted for v0.1.

## ScenarioSpec

YAML and JSON compile to the same immutable bounded `ExecutionPlan`. The `version` field is mandatory and must be exactly `1`; omitted/unknown versions and unknown document members are rejected. The spec declares scenario identity/seed/timeout, a relative ProviderContract path, explicit SUT actions, provider response modes, observations/polling and assertions.

Assertions use RFC 6901 JSON Pointer and closed typed comparators: `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `exists`, `absent`, `one_of`, `count_eq`. There is no implicit type coercion. `validate` rejects unknown invariant kinds/comparators, invalid JSON Pointers, missing/duplicate ids, undeclared observation references and invalid observation/action bounds before any network is opened.

Retries and webhook deliveries are explicit; the engine does not silently add resilience policies.

## ProviderContract

ProviderContract adapts REST JSON shapes to the Paytness domain without executing user code. Its `version` field is also mandatory and must be exactly `1`; unknown members and invalid extraction JSON Pointers are rejected during validation. v1 permits exact HTTP method/path, JSON Pointer extraction, idempotency header, JSON response/webhook skeletons, closed bindings and state/event mappings.

Initial bindings: `providerAttemptId`, `logicalPayment`, `amountMinor`, `currency`, `providerState`, `eventId`, `eventType`, `eventOccurredAt`.

## Rejected

CEL, JsonLogic, JSONPath, arbitrary templating, C#, shell, scripts, loops, callbacks, reflection, remote includes and executable plugins. If two simple external APIs cannot be expressed without turning the contract into a programming language, stop and revisit the compatibility target rather than adding scripting.
