# ADR-006 — Distribution and release surfaces

**Status:** accepted; binary/NuGet surfaces implemented, OCI build prepared, publication intentionally gated.

## Context

Paytness is a developer CLI with an embedded Kestrel provider. Distribution must minimize setup without introducing a Go/.NET hybrid or requiring the .NET runtime for users who choose native-style binaries.

The original Web SDK produced IIS/static-web deployment artifacts that are irrelevant to a CLI and prevented a strict one-file Windows distribution.

## Decision

Use `Microsoft.NET.Sdk` with an explicit `Microsoft.AspNetCore.App` FrameworkReference. Paytness remains ASP.NET Core/.NET 10 but does not use Web SDK deployment conventions.

Distribution surfaces, in order:

1. self-contained single-file binaries for `linux-x64`, `linux-arm64`, `win-x64`, `osx-x64`, and `osx-arm64`;
2. NuGet .NET tool PackageId `Paytness`, command `paytness`;
3. OCI Linux amd64/arm64 image;
4. thin composite GitHub Action that installs the NuGet tool and invokes the CLI.

Single-file publish is untrimmed in v0.1 and sets `IncludeNativeLibrariesForSelfExtract=true` so Windows also ships as one file. Native AOT remains out of scope.

RID restores use isolated ephemeral lockfiles under `.artifacts`; packaging must prove the canonical `src/Paytness/packages.lock.json` hash does not change. The normal project/CI path continues to use locked restore.

The OCI image is framework-dependent and runs on the pinned ASP.NET Core 10.0.12 runtime image. Managed build output is architecture-neutral, so Buildx can create amd64/arm64 images without architecture-specific application builds.

The GitHub Action contains no payment logic and no arbitrary `eval`/free-form argument string. Its setup-dotnet dependency is pinned to a full commit SHA.

## Publication posture

No NuGet package, image, release, workflow or public-repository transition happens merely because packaging works locally. Publication remains blocked until license, public-name checks, secret/history review, full normal-Docker reference validation, and release checklist are complete.

Active repository workflows are intentionally not enabled during the private pre-alpha phase to avoid unnecessary GitHub Actions usage. Local gates remain canonical.

## Evidence

All five RIDs produced exactly one self-contained file. Linux x64 executed `--version` and `validate`. Local NuGet pack/install/execute passed. The Action entrypoint installed the local package and completed a real HTTP healthy scenario with JSON/JUnit output. The OCI payload was executed successfully on `mcr.microsoft.com/dotnet/aspnet:10.0.12`; nested Devbox restrictions prevent a complete Dockerfile build inside the toolbox.
