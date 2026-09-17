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

`reference/nopcommerce/plugin/Paytness.Reference` links to nopCommerce assemblies in order to exercise real nopCommerce payment/application behavior. Original Paytness-authored files are licensed under Apache-2.0, but that grant does not license nopCommerce itself or make a linked/combined nopCommerce plugin or derived image Apache-2.0-only. The project's conservative distribution posture is documented in `reference/nopcommerce/LICENSING.md`: build locally from pinned upstream, preserve attribution, and do not publish a prebuilt nopCommerce-derived image by default.

Any future redistribution of a combined or derived nopCommerce reference artifact requires a fresh review of applicable upstream NPL 4.0 obligations.

## Other dependencies

Paytness also consumes third-party .NET/NuGet dependencies. Their licenses remain governed by their respective upstream packages. Package versions are locked and dependency/security checks are part of the repository verification gates.

This file is an attribution and dependency-boundary notice. Paytness's Apache-2.0 license is in `LICENSE`; scope and the nopCommerce boundary are described in `LICENSING.md`. This notice does not provide legal advice.
