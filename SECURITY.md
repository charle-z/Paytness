# Security

Paytness is a test runner for synthetic data and authorized test/staging targets. It is not a PCI DSS scanner or certification tool.

## Security posture

- loopback is the default listen/target posture;
- private/public targets require explicit operator authorization;
- redirects are disabled and normal platform TLS validation is retained;
- ScenarioSpec cannot execute code or read environment variables;
- secrets are supplied only through explicit CLI-to-environment mappings;
- payment card data is outside the supported canonical model;
- evidence must be sanitized before storage or reporting.

Do not use production PAN/CVV/PIN/track data with Paytness.

## Reporting a vulnerability

Until the repository is public, report security issues privately to the repository owner. Before public launch this document will be updated with a stable private reporting channel and supported-version policy.

## nopCommerce reference

`reference/nopcommerce/` is test-only. Its plugin requires explicit reference mode, PostgreSQL is not published to the host, and nopCommerce is bound to host loopback by Compose. The reference database uses trust authentication only on the ephemeral internal Compose network; this is deliberately not a production deployment pattern. Never deploy the reference plugin or Compose stack to production.

The reference webhook endpoint verifies HMAC-SHA256 over the exact bytes received and performs constant-time signature comparison. The reference signing value is supplied through environment configuration and is not part of ScenarioSpec.
