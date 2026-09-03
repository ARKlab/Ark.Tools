# DOC-01 — Reqnroll testing guidance

## Goal

A contributor can create a Reqnroll project, compose the application without a
transport, write a scenario-owned driver, and then add focused HTTP/gRPC/Rebus
boundary tests without guessing which layer owns an assertion.

## Complete workflow

1. Create an MSTest project and add `Reqnroll.MsTest`,
   `Reqnroll.Tools.MsBuild.Generation`, and `Ark.Tools.Reqnroll`.
2. Add `reqnroll.json` and the repository table-mapping configuration.
3. Reference the API and Application projects; do not reference a web host for
   direct application scenarios.
4. Create one `ApplicationTestContext` per scenario.
5. Choose SQL by default or set `ARK_SAMPLE_INMEMORY_TESTS=1` explicitly.
6. Set a deterministic clock and authenticated test principal.
7. Dispatch request/query/command contracts through a driver.
8. Keep active entities/results in the driver, not static fields.
9. Add a `Then` assertion to every scenario.
10. For Rebus, use independent sender/receiver containers, bounded waits,
    cleanup, and durable business assertions.
11. Add HTTP/gRPC/Functions tests only for wire behavior.

## Ownership table

| Assertion | Test layer |
| --- | --- |
| Handler returns `Greeting.V1.Output` | Application |
| Duplicate greeting raises business violation | Application |
| Greeting is eventually completed by a worker | Application + Rebus integration |
| `POST /api/v1/greetings` returns `201` | HTTP boundary |
| JSON uses camelCase and source-generated metadata | HTTP/serialization |
| gRPC maps validation to `InvalidArgument` | gRPC boundary |
| OpenAPI contains an OAuth scheme | OpenAPI boundary |
| Functions host becomes ready | Functions boundary |

## What not to do

- Do not assert generated wrappers in application features.
- Do not share an `AsyncLocal` current entity between scenarios.
- Do not mock application-owned SQL or message-bus infrastructure.
- Do not replace an application-owned `IFailed<T>` handler with a test handler.
- Do not use an unbounded `Task.Delay` as a Rebus wait.
- Do not swallow exceptions in a `When` step.
- Do not make the last step a `When`.

## Acceptance checks

- [x] `dotnet build samples/Ark.MediatorFramework.Sample/Ark.MediatorFramework.Sample.slnx`
  succeeds.
- [x] The sample test project can run with SQL and with the explicit in-memory
  profile.
- [x] Markdown links in this document and
  [`guide/testing.md`](../../../guide/testing.md) resolve.
- [x] No credential, connection string, or token appears in examples.
- [x] `git diff --check` is clean.

> **Review 2026-09-02**: `docs/mediator-framework/guide/testing.md` exists and covers the two-layer model, Reqnroll setup, scenario-owned context, deterministic clock/principal, the `ARK_SAMPLE_INMEMORY_TESTS=1` profile, and boundary-test ownership; link check clean; full-solution build (0 warnings) and tests (859/859) green on this review.
