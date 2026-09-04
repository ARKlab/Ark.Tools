# Testing a Mediator Framework application

Use two test layers. Mixing them makes failures slow and assertions fragile.

1. **Application behavior tests** dispatch contracts directly through a
   scenario-owned application composition.
2. **Host-boundary tests** start HTTP, gRPC, Rebus, or Functions and verify
   transport behavior.

Reference both the API and Application projects. The API supplies the public
contracts; the Application project supplies handlers and composition. The
sample implements both layers in
[`Ark.MediatorFramework.Sample.Tests`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests).

## 1. Create a Reqnroll test project

Create a test project beside the application:

```bash
dotnet new mstest -n MyApp.Tests
dotnet add MyApp.Tests package Reqnroll.MsTest
dotnet add MyApp.Tests package Reqnroll.Tools.MsBuild.Generation
dotnet add MyApp.Tests package Ark.Tools.Reqnroll
dotnet add MyApp.Tests reference ../MyApp.API/MyApp.API.csproj
dotnet add MyApp.Tests reference ../MyApp.Application/MyApp.Application.csproj
```
Source: [`Ark.MediatorFramework.Sample.Tests.csproj`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Ark.MediatorFramework.Sample.Tests.csproj)

The repository uses central package management. In that model, put the package
versions in `Directory.Packages.props` and omit `Version` from the project:

```xml
<ItemGroup>
  <PackageReference Include="Reqnroll.MsTest" />
  <PackageReference Include="Reqnroll.Tools.MsBuild.Generation" />
  <PackageReference Include="Ark.Tools.Reqnroll" />
</ItemGroup>
```
Source: [`Ark.MediatorFramework.Sample.Tests.csproj`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Ark.MediatorFramework.Sample.Tests.csproj)

Add `reqnroll.json`:

```json
{
  "language": "en",
  "bindingCulture": { "name": "en" },
  "runtime": { "stopAtFirstError": false }
}
```
Source: [`reqnroll.json`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/reqnroll.json)

Use `TableMappingConfiguration` for custom `Reqnroll.Assist` mappings. The
sample's implementation is
[`Init/TableMappingConfiguration.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Init/TableMappingConfiguration.cs).

## 2. Build a scenario-owned application context

The context owns one composition, persistence profile, user, clock, and
transport state for one scenario:

```csharp
public sealed class ApplicationTestContext : IAsyncDisposable
{
    private readonly Container _container;

    public ApplicationTestContext(bool useSqlStore)
    {
        _container = new Container();
        _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        ApplicationComposition.Register(_container, useSqlStore);
        _container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(
            new TestPrincipalProvider());
        _container.Verify();
    }

    public async Task<TResponse> DispatchRequestAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
    {
        using var scope = AsyncScopedLifestyle.BeginScope(_container);
        var handler = _container
            .GetInstance<IRequestHandler<TRequest, TResponse>>();
        return await handler.ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _container.Dispose();
        return ValueTask.CompletedTask;
    }
}
```
Source: [`ApplicationTestContext.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/ApplicationTestContext.cs)

In production code, prefer a reusable test context/driver rather than putting
container resolution in every binding. The sample context adds deterministic
NodaTime clocks, authenticated test users, SQL reset hooks, and Rebus sender and
receiver containers.

## 3. Write a coarse, domain-level driver

```csharp
public sealed class GreetingDriver
{
    private readonly ApplicationTestContext _context;
    private Greeting.V1.Output? _current;

    public GreetingDriver(ApplicationTestContext context)
    {
        _context = context;
    }

    public Greeting.V1.Output Current =>
        _current ?? throw new InvalidOperationException("No greeting is active.");

    public async Task CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        _current = await _context.DispatchRequestAsync
            <Greeting_CreateRequest.V1, Greeting.V1.Output>(
                new Greeting_CreateRequest.V1(
                    new Greeting.V1.Create { Name = name }),
                cancellationToken).ConfigureAwait(false);
    }
}
```
Source: [`BookDriver.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Drivers/BookDriver.cs)

Bindings map Gherkin to drivers. Drivers own mutable scenario state. Do not
share a current entity, container, clock, or message receiver between
scenarios.

## 4. Write the feature

```gherkin
Feature: Greetings

Scenario: Create a greeting
    Given I am an authenticated user with the "greetings.write" scope
    When I create a greeting for "Ada"
    Then the current greeting message is "Hello, Ada!"
```
Source: [`Books.feature`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Features/Books.feature)

The step should describe a user or QA action, not a handler class:

```csharp
[Binding]
public sealed class GreetingSteps
{
    private readonly GreetingDriver _greetings;

    public GreetingSteps(GreetingDriver greetings)
    {
        _greetings = greetings;
    }

    [When("I create a greeting for \"(.*)\"")]
    public async Task CreateGreeting(string name)
    {
        await _greetings.CreateAsync(name).ConfigureAwait(false);
    }

    [Then("the current greeting message is \"(.*)\"")]
    public void CheckMessage(string message)
    {
        _greetings.Current.Message.Should().Be(message);
    }
}
```
Source: [`BookSteps.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Steps/BookSteps.cs)

Every scenario ends with a `Then` or an `And` continuation. A `Given` that
invokes an operation must assert that setup succeeded.

## 5. Test failures correctly

Capture an exception only when the scenario explicitly expects failure:

```csharp
[When("I try to create a greeting without a name")]
public async Task TryCreateWithoutName()
{
    try
    {
        await _context.DispatchRequestAsync
            <Greeting_CreateRequest.V1, Greeting.V1.Output>(
                new Greeting_CreateRequest.V1(new Greeting.V1.Create()))
            .ConfigureAwait(false);
    }
    catch (Exception exception)
    {
        _exception = exception;
    }
}

[Then("the request is rejected by validation")]
public void CheckValidationFailure()
{
    _exception.Should().BeOfType<ValidationException>();
}
```
Source: [`BookSteps.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Steps/BookSteps.cs)

Do not swallow unexpected exceptions or convert failures into a successful
scenario. The driver should preserve enough exception detail for assertions.

## 6. Test Rebus as an owned workflow

Create independent sender and receiver containers. Share only the intentional
test transport and scenario state:

```csharp
await sender.Send(new CompleteGreetingCompositionRequest { Id = id, Name = "Ada" })
    .ConfigureAwait(false);

await receiver.WaitForIdleAsync(TimeSpan.FromSeconds(10))
    .ConfigureAwait(false);

var result = await application.ReadAsync(id).ConfigureAwait(false);
result.Status.Should().Be("completed");
```
Source: [`MessagingBusSampleTests.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/MessagingBusSampleTests.cs)

Use bounded polling. On timeout report queue activity, outbox rows, deferred
messages, and error-queue messages. Dispose receivers and clear test transport
after every scenario.

Assert the durable business effect or external call. Do not assert generated
wrapper types, Rebus headers, or the exact retry implementation unless the
scenario explicitly owns that operational behavior.

## 7. SQL and in-memory profiles

The sample defaults to SQL:

```bash
docker compose -f samples/Ark.MediatorFramework.Sample/docker-compose.yml up -d db
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```
Source: [`DatabaseHooks.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/DatabaseHooks.cs)

The hook deploys the DACPAC once and calls
`[ops].[ResetFull_OnlyForTesting]` before each scenario. Reset uses
foreign-key-safe `DELETE FROM` statements.

Choose in-memory explicitly:

```bash
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test \
  samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```
Source: [`ApplicationTestContext.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/ApplicationTestContext.cs)

Do not silently fall back to memory when SQL setup fails; a profile change can
hide a persistence defect.

## 8. Keep transport tests separate

Application tests assert:

- returned values and persisted state;
- validation, authorization, not-found, and business-rule exceptions;
- audit, paging, ETag, attachment, and cancellation behavior;
- eventual Rebus effects.

HTTP/gRPC/Functions tests assert:

- routes, status codes, headers, auth middleware;
- JSON, MessagePack, protobuf, and ProblemDetails;
- generated OpenAPI and `.proto` output;
- readiness and startup failure behavior.

This prevents an application scenario from duplicating framework tests. The
sample's `CompositionRootTests`, gRPC client tests, and Functions boundary tests
own those transport concerns.

## 9. Run targeted tests

```bash
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests \
  --filter "DisplayName~Create a greeting"

dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests \
  --filter "DisplayName~Rebus"
```
Source: [`MessagingBusSampleTests.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/MessagingBusSampleTests.cs)

Read [DOC-01 testing guidance](../progress/tasks/testing/DOC-01-testing-guidance.md)
for the complete ownership map, cleanup rules, and documentation acceptance
checks.
