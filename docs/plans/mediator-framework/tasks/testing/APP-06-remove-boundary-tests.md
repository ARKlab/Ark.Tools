# APP-06 — Remove obsolete application boundary tests and dependencies

**Depends on:** TST-03, TST-04, TST-05, APP-03, APP-05
**Scope:** Sample tests and solution project graph

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Delete or move the old host-bound `SampleTestContext` implementation,
   transport `GreetingSteps`, generated-client references, and any test that
   exists only to assert URLs, status codes, ProblemDetails, serialization, or
   OpenAPI.
2. Convert tests that contain application behavior mixed with transport setup:
   - `ConcurrencyRoundtripTests` becomes direct update/ETag/concurrency
     contract scenarios.
   - `AsyncEnumerableStreamingTests` becomes direct enumeration/cancellation
     behavior; wire streaming remains in TST-03/TST-04.
   - `FileDownloadTests` becomes direct attachment storage/retrieval behavior;
     multipart/download wire behavior remains in TST-03/TST-04.
   - `PagingTests` becomes direct query validation and result behavior; binding
     shape remains in framework tests.
   - `AuthorizationTests` keeps direct policy/user-context behavior; bearer
     parsing and HTTP/gRPC status behavior moves to framework tests.
3. Remove unused `Microsoft.AspNetCore.TestHost`, `Grpc.Net.Client`,
   generated-client, and host-only package/project references from the sample
   application test project. Retain a package only if another sample-owned
   integration test still needs it and document that owner.
4. Keep Azure Functions sample tests only for sample-owned wiring if D3 selects
   that exception; otherwise move generic trigger behavior to TST-06.
5. Verify no production application file was changed to accommodate a test
   transport.

## Outcome

- The sample test project is a focused application behavior example, not a
  second framework hosting suite.
- OpenAPI, generated endpoint binding, multipart/download wire behavior,
  streaming wire behavior, and transport authorization remain covered by the
  framework hosting tests.
- The sample's forwarded-prefix host policy is intentionally out of scope for
  the application behavior suite; it is a host-composition concern rather than
  application or mediator behavior.

## Acceptance

- [x] A repository search finds no HTTP/gRPC client or `TestServer` in the
  direct application BDD suite.
- [x] The removed dependencies are absent unless a documented sample-owned test
  still requires them.
- [x] Every deleted assertion is represented by a framework-owned test or is
  explicitly listed as intentionally out of scope.
- [x] The sample still demonstrates every application behavior in APP-03 and
  APP-04.

## Tests

- Run
  `dotnet list samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Ark.MediatorFramework.Sample.Tests.csproj package`
  and inspect the remaining test dependencies.
- Run the sample test project and the framework hosting projects.
- Required scenarios/cases:
  - every removed transport assertion has a framework-hosting counterpart or an
    explicit out-of-scope record;
  - the direct application suite contains no `HttpClient`, gRPC client,
    `TestServer`, URL, status, ProblemDetails, serialization, or OpenAPI
    assertion;
  - APP-03 and APP-04 behavior remains covered after dependency removal.
- Run the full-solution gates.
