# Third-party notices

Paytness core does not vendor or embed nopCommerce binaries in its normal CLI, NuGet, or OCI distribution surfaces. The repository does include a **test-only reference integration** under `reference/nopcommerce/` that is built locally against the official nopCommerce 4.90.8 image.

## nopCommerce 4.90.8

- Upstream project: nopCommerce
- Reference version: 4.90.8
- Upstream license: nopCommerce Public License Version 4.0 (NPL 4.0)
- NPL 4.0 is described by nopCommerce as GNU Affero General Public License version 3 plus additional nopCommerce terms, including visible `powered by nopCommerce` attribution requirements.
- Upstream license information: `https://www.nopcommerce.com/license`

The Paytness reference harness does not remove upstream attribution and does not publish a prebuilt nopCommerce-derived image as a normal Paytness release artifact. Instead, the reference image is assembled locally from the official upstream image when the test harness runs.
The test-only payment-info component also renders a visible clickable `powered by nopCommerce` attribution linking to `https://www.nopcommerce.com` as conservative license hygiene. This does not represent a legal conclusion that the component alone satisfies every NPL obligation.

`reference/nopcommerce/plugin/Paytness.Reference` links to nopCommerce assemblies in order to exercise real nopCommerce payment/application behavior. The applicable licensing treatment for that test-only plugin is a **separate publication decision** and is not established by this notice. Do not assume that a future root Paytness license automatically governs that plugin or overrides nopCommerce licensing obligations.

Before public release, the repository publication checklist requires explicit review of the license/notice treatment for `reference/nopcommerce/`.

## Other dependencies

Paytness also consumes third-party .NET/NuGet dependencies. Their licenses remain governed by their respective upstream packages. Package versions are locked and dependency/security checks are part of the repository verification gates.

This file is an attribution and dependency-boundary notice; it is not a substitute for the repository's eventual `LICENSE` file and does not provide legal advice.
