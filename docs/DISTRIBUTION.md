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

The script uses an isolated NuGet lock per RID under `.artifacts`, asserts that the canonical project lock hash is unchanged, smokes the Linux x64 binary, builds/installs/smokes the .NET tool, creates `.tar.gz` archives and regenerates `SHA256SUMS`.

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

The root `Dockerfile` builds a framework-dependent Paytness payload and runs it on the pinned ASP.NET Core 10.0.12 image as the image's non-root app user.

With Docker Buildx:

```sh
./eng/package-oci.sh
```

The script produces a Linux amd64/arm64 OCI archive under the same versioned `dist/` directory and regenerates checksums.

The nested Devbox toolbox cannot execute Dockerfile `RUN` instructions because its parent sandbox denies supplemental-group operations. The framework-dependent application payload itself has been run successfully inside `mcr.microsoft.com/dotnet/aspnet:10.0.12`. A complete Docker/Buildx run on a normal Docker host remains a publication gate.

## GitHub Action

`action.yml` is a thin composite wrapper. It installs .NET 10 using a SHA-pinned `actions/setup-dotnet`, installs the requested Paytness NuGet tool version, validates the caller ScenarioSpec, then invokes `paytness run`.

The action does not accept an arbitrary shell/free-form args input. Target/public-listen/security opt-ins are explicit inputs, and webhook secrets remain caller environment variables whose **name**, not value, is passed to Paytness.

The action entrypoint has been tested locally with a locally packed NuGet tool against a real HTTP SUT, including JSON/JUnit reports and GitHub-style outputs. The action is not usable from a public tag until the repository and corresponding NuGet version are published.

## Canonical gates

- fast local/product gate: `./eng/verify.sh`
- real nopCommerce gate: `./reference/nopcommerce/run.sh`
- binary/NuGet packaging: `./eng/package.sh`
- OCI packaging: `./eng/package-oci.sh`

Publication automation must call these scripts instead of duplicating their command lists.
