# ADR-001 — Runtime and process architecture

Status: Accepted for v0.1.

## Decision

Use C# 14 on .NET 10 LTS. Paytness is one executable and one process containing CLI, embedded Kestrel provider, scenario execution, scheduling, observation, evidence and reporting. Runtime state is in-memory and discarded after each run.

Logical modules: `Cli`, `Scenario`, `Domain`, `Execution`, `Provider`, `Webhooks`, `Observation`, `Security`, `Evidence`, `Reporting`.

Start with one main project plus tests. Do not create a multi-project Clean Architecture hierarchy without a concrete boundary that requires it.

## Concurrency

Use `async/await`, a root `CancellationToken`, bounded `Channel<ScheduledAction>`, a single scheduler reader feeding `PriorityQueue<ScheduledAction, ScheduleKey>`, `SemaphoreSlim` for outbound concurrency (32 default, 128 hard maximum), and short per-payment aggregate locks with no `await` while held. Use `TimeProvider`; fake time is internal-test-only.

## Rejected for v0.1

Go/.NET hybrid, microservices, sidecars, runner database, Controllers/MVC/Razor/Blazor, BackgroundService as generic architecture, MediatR, MassTransit, Hangfire, Dapr, Orleans and EF Core.

A future sidecar may be evaluated only if Kestrel cannot model a transport failure required by the product and that limitation is demonstrated experimentally.
