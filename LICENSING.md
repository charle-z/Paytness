# Paytness licensing

## Paytness core and normal distribution surfaces

Original Paytness code and documentation are licensed under the Apache License 2.0 unless a file or subtree states otherwise. The canonical license text is in `LICENSE`; package/image metadata uses the SPDX expression `Apache-2.0`.

The normal Paytness release surfaces — self-contained binaries, the `Paytness` NuGet tool, the Paytness OCI image and the thin GitHub Action — contain Paytness core, not nopCommerce. Release packaging includes the Apache-2.0 license text and this licensing notice.

Third-party dependencies remain under their own licenses. `THIRD_PARTY_NOTICES.md` records important dependency/reference boundaries; Apache-2.0 does not grant rights in third-party software or replace its terms.

## nopCommerce reference boundary

`reference/nopcommerce/` is a test-only integration boundary for nopCommerce 4.90.8. Paytness-authored files in that subtree remain original Paytness work, but using or distributing them as a nopCommerce plugin/reference does not make nopCommerce or the combined work Apache-2.0-only. nopCommerce 4.90.8 is distributed upstream under the nopCommerce Public License 4.0 (NPL 4.0), described by nopCommerce as AGPLv3 plus additional nopCommerce terms.

Paytness therefore does not publish a prebuilt nopCommerce-derived image as a normal release artifact. The reference image is assembled locally from the official upstream image, visible upstream attribution is preserved, and redistribution of a combined/derived reference artifact requires a fresh review of then-current upstream obligations.

See `reference/nopcommerce/LICENSING.md` and `THIRD_PARTY_NOTICES.md`. This records the project's release posture; it is not legal advice.
