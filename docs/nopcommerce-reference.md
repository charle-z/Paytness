# nopCommerce 4.90.8 reference

The reference SUT is **nopCommerce 4.90.8 + PostgreSQL 16**. It exists to prove that Paytness can detect payment-reliability failures in a real external .NET application, not only in the synthetic test SUT.

Paytness core remains .NET 10 and in-memory. It never reads the nopCommerce database and contains no nopCommerce-specific code.

## One-command reference gate

Prerequisite: Docker with the Compose plugin. On Windows, run this from WSL.

```sh
./reference/nopcommerce/run.sh
```

The script builds the two reference images, creates a fresh PostgreSQL database, waits for nopCommerce readiness, runs NOP-01..07, recreates the stack for each required mutation, verifies that both mutations are detected, writes JSON/JUnit reports under `.artifacts/nopcommerce/`, and tears the stack down automatically.

Set `PAYTNESS_KEEP_REFERENCE=1` only when debugging and you intentionally want the stack left running after the gate.

## Architecture

```text
Paytness runner/container
    |  provider HTTP + signed webhooks
    v
nopCommerce 4.90.8
    |  test-only Paytness.Reference plugin
    v
normal nopCommerce services/events
    |
    v
PostgreSQL 16
```

The plugin exposes only test/reference surfaces:

- `POST /test/pay` — driver endpoint;
- `POST /test/webhook` — HMAC-verified webhook receiver;
- `GET /test/state` — observer endpoint.

`ReferencePaymentMethod` is a real nopCommerce `IPaymentMethod`. The driver creates a real `Order`, invokes nopCommerce `IPaymentService`, and transitions payment through `IOrderProcessingService.MarkOrderAsPaidAsync`. `ReferenceOrderPaidConsumer` counts the real `OrderPaidEvent` independently of the driver.

## Zero-click bootstrap

No browser installation flow is required.

The derived nopCommerce image contains PostgreSQL DataConfig plus the test-only plugin. PostgreSQL creates the `citext` extension at initialization. On an empty database, nopCommerce runs its normal FluentMigrator migrations. Before the request pipeline reaches the first component that requires a Store, `ReferenceStartup` invokes nopCommerce's own `IInstallationService.InstallAsync` to seed required application data.

The bootstrap credential is generated in memory for that startup and is never printed or stored by Paytness. The reference plugin replaces `IWebHelper` only in explicit reference mode so the official seed service can determine its initial store URL before a Store exists.

This path was tested from an empty database: migrations completed, one Store was created, customers were seeded, the plugin loaded, and the nopCommerce home page returned HTTP 200.

## Required scenarios and verified results

| Scenario | Verified invariant |
| --- | --- |
| NOP-01 Healthy | 1 request, 1 attempt, 1 economic effect, Order `Paid`, 1 `OrderPaidEvent` |
| NOP-02 Response lost after commit | 1 request/attempt/effect; nopCommerce remains `Pending`, 0 paid events |
| NOP-03 Stable retry | 2 requests, 1 attempt/effect, 1 idempotency key, final `Paid`, 1 paid event |
| NOP-04 Duplicate webhook | same event delivered twice, 2 ACKs, 1 application, final `Paid`, 1 paid event |
| NOP-05 Out of order | `succeeded` delivered before stale `processing`; final remains `Paid` |
| NOP-06 Eventual convergence | ambiguous response followed by delayed succeeded webhook converges to `Paid` |
| NOP-07 Concurrent same payment | 2 concurrent provider requests, 1 attempt/effect/key, final `Paid`, 1 paid event |

All seven have passed end-to-end against the real nopCommerce 4.90.8 image and PostgreSQL 16 in the Devbox reference environment.

## Required mutation gates

`PAYTNESS_REFERENCE_MUTATION=unstable-idempotency` deliberately changes the retry idempotency key. NOP-03 must fail. Verified result: 2 requests, 2 attempts, 2 economic effects, 2 distinct keys, including:

```text
[Fail] one-economic-effect: expected=1 actual=2
```

`PAYTNESS_REFERENCE_MUTATION=accept-stale-state` deliberately allows a stale `payment.processing` webhook to regress an already paid order. NOP-05 must fail. Verified result:

```text
[Fail] paid: expected=Paid actual=Pending
```

Each mutation was tested after recreating the database so a previous successful run could not mask the failure.

## Security and isolation

The reference is explicitly test-only. `PAYTNESS_REFERENCE_MODE=1` is required for its driver/bootstrap behavior. PostgreSQL is not published to the host by Compose. The Compose database uses trust authentication only inside this ephemeral reference network; it is not a production deployment template. nopCommerce is published only on host loopback (`127.0.0.1:8080`). Webhooks are HMAC-SHA256 verified over the exact bytes received and the reference signing key may be overridden through the environment.

Never deploy the reference plugin or its Compose configuration to production.

## Reproducible build

`Dockerfile.nopcommerce` derives from `nopcommerceteam/nopcommerce:4.90.8` and copies the exact nopCommerce assemblies from that same image into the plugin build stage. The repository therefore does **not** version nopCommerce DLLs.

`Dockerfile.paytness` builds Paytness with SDK 10.0.401 and runs it on ASP.NET Core runtime 10.0.12. Dependency restore is locked.

### Devbox validation note

The product behavior above was executed with real nopCommerce/PostgreSQL containers and both mutation gates passed. The final public Dockerfiles/Compose are also structurally validated, but the nested rootless Devbox toolbox cannot execute Dockerfile `RUN` steps because its parent sandbox denies container `setgroups`/supplemental-group operations. This is a limitation of the nested validation harness, not a failure observed in Paytness or nopCommerce. The public reference path targets a normal Docker/Compose installation.

## Rejection criteria

The reference design must be revisited if it ever requires Paytness core to query PostgreSQL, patch nopCommerce core, embed nopCommerce concepts in the runner, or require manual browser/admin-panel setup to execute the gate.
