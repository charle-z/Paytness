# nopCommerce reference licensing boundary

This subtree is a **test-only reference integration** for nopCommerce 4.90.8. It is not part of Paytness's normal binary, NuGet or OCI distribution payload.

## Paytness-authored files

Original Paytness-authored source, scripts and documentation in this subtree are covered by Paytness's Apache-2.0 license as original works. That grant does **not** license nopCommerce itself and does not remove or supersede obligations that apply when these files are linked, combined, distributed or used with nopCommerce.

## Upstream nopCommerce

nopCommerce 4.90.8 is distributed upstream under the nopCommerce Public License 4.0 (NPL 4.0), which nopCommerce describes as AGPLv3 plus additional nopCommerce terms. The authoritative upstream license information is published at `https://www.nopcommerce.com/license`.

The reference plugin links against nopCommerce assemblies and the local reference image derives from the official nopCommerce image. Paytness therefore does not claim that a combined plugin/image is Apache-2.0-only. Anyone redistributing a combined or derived reference artifact must review and satisfy applicable upstream NPL 4.0 obligations.

## Distribution posture

Paytness's default release process intentionally:

- does **not** publish a prebuilt nopCommerce-derived image;
- builds the reference locally from the official pinned upstream image;
- keeps nopCommerce DLL staging and generated reference artifacts ignored;
- preserves visible `powered by nopCommerce` attribution in the test-only plugin;
- keeps Paytness core independent of nopCommerce and PostgreSQL internals.

If this posture changes, publication must stop until the licensing boundary is reviewed again.
