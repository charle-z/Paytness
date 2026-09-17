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

The OCI application payload is framework-dependent and architecture-neutral. It is published before the container build with the pinned .NET SDK, then a runtime-only Dockerfile (no SDK and no `RUN`) copies it onto pinned ASP.NET Core 10.0.12. Buildx exports an OCI Image Layout directory with normalized timestamps; Trivy scans each target platform from the layout before a deterministic release tar is created. Buildx can therefore assemble amd64/arm64 images without architecture-specific application builds or executing build steps inside the image builder.

The GitHub Action contains no payment logic and no arbitrary `eval`/free-form argument string. Its setup-dotnet dependency is pinned to a full commit SHA.

## Publication posture

No NuGet package, image, release, workflow or public-repository transition happens merely because packaging works locally. Publication remains blocked until license, public-name checks, secret/history review, full normal-Docker reference validation, and release checklist are complete.

Active repository workflows are intentionally not enabled during the private pre-alpha phase to avoid unnecessary GitHub Actions usage. Local gates remain canonical.

## Evidence

All five RIDs produced exactly one self-contained file. Linux x64 executed `--version` and `validate`. Local NuGet pack/install/execute passed. The Action entrypoint installed the local package and completed a real HTTP healthy scenario with JSON/JUnit output. The OCI payload was executed successfully on `mcr.microsoft.com/dotnet/aspnet:10.0.12`. Trivy 0.74.0 successfully scanned the x64 runtime image via both Docker archive and OCI Image Layout input with zero HIGH/CRITICAL findings in the observed environment. After moving to a runtime-only Dockerfile, the actual image also builds inside nested Devbox. Its declared non-root UID cannot be executed in that toolbox because only one UID/GID is mapped, so native non-root runtime smoke plus multiarch Buildx/Trivy remains a normal-host release gate.
