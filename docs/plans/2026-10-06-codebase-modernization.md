# Codebase Modernization Plan (October 2026)

**Goal:** Track the follow-ups from the October 2026 modernization review. Each
item has a verdict, a scope and, when there is work, its own pull request.

**Status legend:** `done` (merged or nothing to do), `in progress` (PR open),
`deferred` (valid idea, intentionally not scheduled).

| # | Item | Status | PR |
|---|------|--------|----|
| 1 | Injectable, monotonic time in time-dependent logic | done for the OTel sampler; other sites kept or deferred (see below) | included in this PR (merged from #1024) |
| 2 | `[GeneratedRegex]` for constant patterns | done, nothing to do | none |
| 3 | `System.Threading.Lock` for dedicated lock objects | done, nothing to do | none |
| 4 | `[LoggerMessage]` source-generated logging | done, nothing to do | none |

Review items 5 (sync-over-async wrappers) and 6 (non-`async` `Task` methods) were
dropped from this plan by maintainer decision. This is a scoping call, not a
waiver: the AGENTS.md rule to use `async`/`await` instead of returning a `Task`
directly still applies to new and changed code.

---

## 1. Injectable, monotonic time

**Decision:** use the time abstraction each package already uses. That is NodaTime
`IClock` where the package references NodaTime, Rebus `IRebusTime` inside Rebus
pipeline steps and sagas, and BCL `TimeProvider` where neither is referenced. A
new clock parameter is optional or comes in an added overload, so existing
callers and containers keep working.

### Site triage

| Site | Verdict | Reason |
|------|---------|--------|
| `Ark.Tools.OTel/OperationBucket.cs`, `ArkAdaptiveSampler.cs` | **change** | Rate limiting measures elapsed time with `DateTime.UtcNow`. A wall-clock jump (NTP sync, manual change) refills buckets wrongly or stalls the adaptive controller. Use `TimeProvider` monotonic timestamps; the injectable provider also makes the token bucket testable. |
| `MediatorFramework.Rebus/RebusMessagingBus.cs` `Defer(dueTime)` | keep | Converts an absolute due time to a broker delay. The broker runs on real time, so a fake clock here would schedule wrong deliveries. |
| `MediatorFramework.Messaging.Azure/StorageQueueMessagingTransport.cs` `_scheduledDelay` | keep | Same reason as above. |
| `MediatorFramework.Messaging/MessagingBus.cs` | keep | Already injectable through its `Func<DateTimeOffset>` parameter. |
| `Ark.Tools.Auth0/AuthenticationApiClientCachingDecorator.cs` | keep | Compares a real token expiry with real time to size the cache entry. |
| `Ark.Tools.Rebus/Retry/ArkDefaultRetryStep.cs`, `RavenDb.Auditing` decorator, `OTel/ArkTelemetryFileCollector.cs` | keep | Timestamps that record when something happened; real time is correct. |
| `Ark.Tools.Reqnroll/Auth/JwtTokenBuilder.cs` | keep | Test helper issuing tokens valid against real time. |
| `Compliance.Analyzers` (`DeclarationComplianceAnalyzer`, `SinkTaintAnalyzer`) | keep | `netstandard2.0` Roslyn analyzers; `SinkTaintAnalyzer` already has a time seam. |
| `Ark.Tools.Activity` `SliceActivitySaga` / `SliceActivitySagaData.IsCoolDown` | deferred | A clock constructor parameter is a breaking change for SimpleInjector registrations, and `IsCoolDown` is a public property on persisted saga data. Revisit in the next major version. |
| `Ark.Tools.ResourceWatcher/ResourceWatcher.cs` `_runOnce` | deferred | Testability only, and no current test needs it. Add a `TimeProvider` overload when a test does. |
| `Ark.Tools.EventSourcing/Aggregates/AggregateRoot.cs` | deferred | Injecting a clock into aggregates changes the domain model API. |

### Tasks

- [x] `OperationBucket`: take a `TimeProvider`; track `GetTimestamp()` and compute elapsed time with `GetElapsedTime`.
- [x] `ArkAdaptiveSampler`: add a public `(options, failedTraceRegistry, timeProvider)` constructor; existing constructors use `TimeProvider.System`. Use monotonic timestamps in `_adjustRate` and `Task.Delay(..., timeProvider)` in the controller loop.
- [x] Test with a hand-written `TimeProvider` subclass (no new dependency): the bucket burst is exhausted with frozen time and refills only when time advances.
- [x] Build `Ark.Tools.OTel`; run `tests/Ark.Tools.OTel.Tests`.

## 2. `[GeneratedRegex]` for constant patterns

**Verdict:** nothing to do. The review listed `Ark.Tools.Reqnroll/TableExtensions.cs`
as a candidate, but it already uses `[GeneratedRegex]`. The remaining
constant-pattern sites are in `netstandard2.0` Roslyn generator projects
(`MediatorFramework.AzureFunctions.Generators`, `MediatorFramework.MinimalApi.Generators`),
where `GeneratedRegexAttribute` does not exist. `TestDataComplianceAnalyzer` builds
its pattern at runtime.

**Why Meziantou MA0110 did not warn:** it did not miss anything. MA0110 is
configured as `warning` in `Ark.Tools.MeziantouAnalyzer.globalconfig` and is
active. The rule only reports when the compilation contains
`System.Text.RegularExpressions.GeneratedRegexAttribute`, which means .NET 7 or
later. Verified with a scratch project that references `Meziantou.Analyzer`
3.0.291 and the repository globalconfig, multi-targeting `netstandard2.0;net10.0`:
the same `Regex.IsMatch("…")` and `new Regex("…")` calls raise MA0110 only for
`net10.0`. The one `net10.0` site with a constant pattern
(`tests/Ark.Tools.Compliance.Analyzers.Tests/TestDataAnalyzerTests.cs`) suppresses
MA0110 on purpose: its 1-tick timeout is a deterministic test seam.

## 3. `System.Threading.Lock`

**Verdict:** nothing to do. Every dedicated lock field in `src` is already declared
as `Lock`. The review's count came from grepping the fully qualified type name;
the code uses the short `Lock` name. The only `lock` statements on other objects
are in the test helper `Ark.Tools.Rebus/Tests/TestsInMemoryTimeoutManager.cs`,
which locks on the collections it guards. That is correct and not worth churn.

## 4. `[LoggerMessage]`

**Verdict:** nothing to do. Libraries log through NLog, where `[LoggerMessage]`
does not apply; the AGENTS.md rule for structured NLog calls with
`CultureInfo.InvariantCulture` covers them. Code that uses
`Microsoft.Extensions.Logging` already uses `[LoggerMessage]`
(`SingletonBackgroundService`, `ArkAzureFunctionsEasyAuthHandler`), and
`AnalysisLevel=latest-all` with warnings as errors makes CA1848 enforce it.
The two intentional exceptions are the trace-level `LogTrace` calls in
`BasicAuthAuth0ProxyMiddleware` and `BasicAuthAzureActiveDirectoryProxyMiddleware`,
which suppress CA1848 locally with a justification.
