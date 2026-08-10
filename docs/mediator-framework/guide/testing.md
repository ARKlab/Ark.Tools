# Application testing

Test application behavior through the contracts that handlers implement. The
sample application is the reference for this approach: each Reqnroll scenario
owns its application composition, persistence profile, deterministic clock, and
external-service bindings.

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

Application scenarios should assert:

- returned contract values and persisted business state;
- typed validation, authorization, not-found, and business-rule exceptions;
- deterministic audit entries, concurrency behavior, paging, attachments, and
  streaming cancellation;
- eventual effects and retry/dead-letter outcomes for asynchronous Rebus
  workflows.

Do not resolve a persistence context from a scenario step to arrange or assert
behavior. Use an earlier contract dispatch or a dedicated binding driver. Keep
URLs, status codes, headers, serialized payloads, OpenAPI documents, and
generated-wrapper details out of application scenarios.

## Rebus testing guidance

Assert business outcomes rather than generated wrapper internals. Verify that
sending a message produces the durable effect or external call the workflow
promises. If the application has an `IFailed<T>` handler, it is an application
internal and must not be replaced by a test handler; observe its external
effect or reread the affected entity after the bus is idle.

## Test the exceptional paths

Use typed exception assertions for application failures. Arrange failures
through scenario-scoped external-service mocks and verify both matching calls
and call counts. A mocked singleton must resolve the current scenario binding,
must fail when called outside a scenario, and must never register the real
external service when test configuration is incomplete.

Architecture rationale: [application architecture](application-architecture-best-practices.md).
