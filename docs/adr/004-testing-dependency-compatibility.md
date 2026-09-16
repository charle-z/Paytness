# ADR-004 — FsCheck integration with xUnit v3

Status: Accepted after implementation evidence.

## Context

The original test dependency set specified `xunit.v3` 4.0.0 together with `FsCheck.Xunit` 3.4.0. Both exact versions restore from NuGet, but they are not compile-compatible: `FsCheck.Xunit` 3.4.0 transitively brings xUnit 2.4.1 assemblies. As soon as tests use `[Fact]`, the compiler reports CS0433 because `FactAttribute` exists in both xUnit 2 and xUnit v3.

## Decision

Keep `xunit.v3` 4.0.0 and use the base `FsCheck` 3.4.0 package directly. Property tests run from ordinary xUnit v3 facts via FsCheck's programmatic API (`Check.QuickThrowOnFailure`).

## Impact

Property-based testing remains available at the exact FsCheck version intended. The only removed component is the xUnit 2-specific FsCheck adapter. This avoids running two incompatible xUnit generations in one test assembly and requires no production-code change.
