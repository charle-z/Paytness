# nopCommerce 4.90.8 reference

The reference SUT is **nopCommerce 4.90.8 + PostgreSQL 16**. It exists to prove that Paytness can detect payment-reliability failures in a real external .NET application, not only in the synthetic test SUT.

Paytness core remains .NET 10 and in-memory. It never reads the nopCommerce database and contains no nopCommerce-specific code.

## One-command reference gate

Prerequisite: Docker with the Compose plugin. On Windows, run this from WSL.

```sh
./reference/nopcommerce/run.sh
```

The script uses short-lived SDK containers to compile Paytness and the test-only plugin into ignored `.artifacts`, assembles two runtime-only local images, creates a fresh PostgreSQL database, waits for nopCommerce readiness, runs NOP-01..07, recreates the stack for each required mutation, verifies that both mutations are detected, writes JSON/JUnit reports under `.artifacts/nopcommerce/`, and tears the stack down automatically. No local .NET SDK is required.

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

The locally assembled nopCommerce reference image contains PostgreSQL DataConfig plus the test-only plugin. PostgreSQL creates the `citext` extension at initialization. On an empty database, nopCommerce runs its normal FluentMigrator migrations. Before the request pipeline reaches the first component that requires a Store, `ReferenceStartup` invokes nopCommerce's own `IInstallationService.InstallAsync` to seed required application data.

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

## Reproducible local build

`build-images.sh` requires only Docker from the developer. It creates short-lived `mcr.microsoft.com/dotnet/sdk:10.0.401` containers to perform locked restore/build outside the final images. The exact `Nop.Core`, `Nop.Data`, `Nop.Services`, and `Nop.Web.Framework` assemblies are copied from the official `nopcommerceteam/nopcommerce:4.90.8` image into ignored staging and are never committed.

Both final Dockerfiles are runtime-only and contain **no `RUN` instruction**. `Dockerfile.nopcommerce` layers the plugin/configuration onto the official nopCommerce image; `Dockerfile.paytness` layers the already-published Paytness payload and scenarios onto ASP.NET Core 10.0.12. The readiness check uses `wget` already present in the upstream nopCommerce image, so no package-manager step is required.

The runtime-only packaging path was validated in nested Devbox from a fresh database: both final images built, nopCommerce bootstrapped automatically, and NOP-01 Healthy plus NOP-03 stable retry passed using outputs compiled exclusively by the short-lived SDK containers. The complete NOP-01..07 + both-mutation behavior had already been executed against the same reference code before this packaging refactor. A normal Docker host must rerun the full one-command gate before publication.

## Licensing boundary

nopCommerce 4.90.8 is licensed upstream under NPL 4.0 (AGPLv3 plus nopCommerce additional terms). Original Paytness-authored reference files remain Apache-2.0, but that does not license nopCommerce or make the linked/combined reference Apache-2.0-only. Paytness therefore does **not** publish a prebuilt nopCommerce-derived image as a normal distribution artifact; the reference image is built locally from the official upstream image. Upstream attribution, including applicable `powered by nopCommerce` requirements, must be preserved. See `reference/nopcommerce/LICENSING.md` for the explicit boundary.
The plugin payment-info component includes a visible clickable `powered by nopCommerce` link, and the release metadata gate checks that the text and upstream URL remain present. This is a conservative safeguard; redistribution of combined/derived reference artifacts still requires review of applicable upstream NPL 4.0 obligations.

## Rejection criteria

The reference design must be revisited if it ever requires Paytness core to query PostgreSQL, patch nopCommerce core, embed nopCommerce concepts in the runner, or require manual browser/admin-panel setup to execute the gate.
