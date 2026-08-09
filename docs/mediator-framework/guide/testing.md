# Testing

Test application behavior through the application contracts that handlers
implement. Keep generated endpoint, serialization, authentication, and host
configuration checks in framework-owned tests under `tests/`.

## Application behavior tests

The sample's
`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/ApplicationTestContext.cs`
creates a scenario-owned `SimpleInjector` composition and calls
`ApplicationComposition.Register`. It provides deterministic clocks and users,
the selected SQL or in-memory persistence profile, and the outbound Rebus
composition.

Dispatch a request, query, or command directly:

```csharp
await using var context = new ApplicationTestContext(useSqlStore: false);
context.SetAuthenticatedUser("test-user", ApplicationScopes.GreetingWrite);

var greeting = await context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
    new CreateGreetingRequest { Name = "Ada" });
```

Application tests should assert:

- returned contract values and persisted business state;
- typed validation, authorization, not-found, and business-rule exceptions;
- deterministic audit entries, concurrency behavior, paging, attachments, and
  streaming cancellation;
- eventual effects and retry/dead-letter outcomes for asynchronous Rebus
  workflows.

Do not resolve stores to arrange or assert behavior in a scenario. Use an
earlier contract dispatch or a documented test adapter. Do not assert URLs,
status codes, headers, serialized payloads, OpenAPI documents, or generated
wrapper internals.

## Framework hosting tests

Framework capability tests under
`tests/Ark.Tools.MediatorFramework.Hosting.Tests/` own the transport boundary.
They use synthetic contracts and may use `TestServer`, generated gRPC clients,
and in-memory Rebus processors.

These tests cover:

- generated route, query, body, multipart, server-set, version, and streaming
  binding;
- HTTP status semantics, ProblemDetails, content negotiation, and auth
  middleware;
- exported `.proto` shape, generated gRPC clients, rich errors, metadata, and
  cancellation;
- generated Rebus wrappers, scope, retry, and dead-letter behavior.

The sample's `CompositionRootTests` remains a narrow sample-owned host
composition check. It does not replace framework hosting coverage.

## Rebus testing guidance

Assert business outcomes rather than generated wrapper internals. Application
tests should verify that sending a message produces the durable effect, outbox
entry, or dead-letter behavior the workflow promises. Framework tests verify
message registration, scope creation, retry, and transport mechanics.

## Test the exceptional paths

Use typed exception assertions for application failures. In framework hosting
tests, issue malformed and unauthorized requests, exceed upload limits, cancel
streamed calls, and assert the documented wire result. Keep pure business-rule
tests with the application and generator/transport capability tests with the
framework.

Architecture rationale: [design.md](../design.md).
