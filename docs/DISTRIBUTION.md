# Paytness distribution

Paytness v0.1 prepares four distribution surfaces from the same CLI implementation. None are published remotely during the private pre-alpha phase.

## Self-contained binaries

Run from Linux/WSL:

```sh
./eng/package.sh
```

This publishes and verifies one self-contained file for each supported RID:

- `linux-x64`
- `linux-arm64`
- `win-x64`
- `osx-x64`
- `osx-arm64`

The script uses an isolated NuGet lock per RID under `.artifacts`, asserts that the canonical project lock hash is unchanged, and smokes both the Linux x64 binary and the locally installed .NET tool from an isolated working directory containing only a minimal ScenarioSpec + ProviderContract. It then creates deterministic `.tar.gz` archives, builds the NuGet package with a deterministic timestamp derived from the Git commit, and regenerates `SHA256SUMS`.

Release artifacts are written below `dist/v<version>/` and are intentionally ignored by Git.

The v0.1 binaries are self-contained and untrimmed. Native libraries are bundled for self-extraction so Windows also distributes as one `.exe`. Native AOT is not part of v0.1.

## .NET tool

Local packaging is part of `eng/package.sh`. The package identity is `Paytness`; the command is `paytness`.

Once a version is actually published to NuGet, the intended installation UX is:

```sh
dotnet tool install --global Paytness --version <version>
paytness --version
```

Do not document an unpublished version as installable from NuGet.

## OCI multiarch

The release path first publishes a framework-dependent Paytness payload with the pinned SDK (`eng/prepare-oci-context.sh`). The root `Dockerfile` is runtime-only: it contains no build toolchain and no `RUN` instruction, and runs the staged payload on pinned ASP.NET Core 10.0.12 as the image's non-root app user.

With Docker Buildx:

```sh
./eng/package-oci.sh
```

The script first exports a Linux amd64/arm64 **OCI Image Layout directory** with `tar=false`, derives `SOURCE_DATE_EPOCH` from the Git commit, and asks the OCI exporter to rewrite layer timestamps. Pinned Trivy scans that layout separately for `linux/amd64` and `linux/arm64`; only after both HIGH/CRITICAL scans pass does the script create the deterministic `.oci.tar` release artifact and regenerate checksums.

The runtime-only Dockerfile can be built inside the nested Devbox toolbox because it has no `RUN` instructions. Devbox cannot execute the resulting image under its declared non-root UID because the parent sandbox exposes only a single UID/GID mapping; that is a harness limitation. The payload can be smoke-run with a user override, while the declared non-root image and complete amd64/arm64 Buildx + Trivy path remain publication gates on a normal Docker host.

## GitHub Action

`action.yml` is a thin composite wrapper. It installs .NET 10 using a SHA-pinned `actions/setup-dotnet`, installs the requested Paytness NuGet tool version, validates the caller ScenarioSpec, then invokes `paytness run`.

The action does not accept an arbitrary shell/free-form args input. Target/public-listen/security opt-ins are explicit inputs, and webhook secrets remain caller environment variables whose **name**, not value, is passed to Paytness.

The action entrypoint has been tested locally with a locally packed NuGet tool against a real HTTP SUT, including JSON/JUnit reports and GitHub-style outputs. The action is not usable from a public tag until the repository and corresponding NuGet version are published.

## Canonical gates

- fast local/product gate: `./eng/verify.sh`
- controlled B7/performance gate: `./eng/performance-gates.sh`
- real nopCommerce gate: `./reference/nopcommerce/run.sh`
- binary/NuGet packaging: `./eng/package.sh`
- bit-reproducibility gate: `./eng/package-repro-check.sh`
- OCI packaging: `./eng/package-oci.sh`

Publication automation must call these scripts instead of duplicating their command lists.
