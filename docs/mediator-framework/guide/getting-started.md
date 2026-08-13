# Getting started: one Ping

This chapter builds the smallest useful service. It has one public contract,
one handler, one SimpleInjector container, and one generated HTTP endpoint.
Copy the code into a new `net10.0` ASP.NET Core project, then compare each step
with the larger sample.

## 1. Add the packages

Reference:

```xml
<ItemGroup>
  <PackageReference Include="Ark.Tools.MediatorFramework" />
  <PackageReference Include="Ark.Tools.MediatorFramework.MinimalApi" />
  <PackageReference Include="Ark.Tools.Solid" />
</ItemGroup>
```

`Ark.Tools.MediatorFramework` supplies shared framework metadata.
`Ark.Tools.MediatorFramework.MinimalApi` supplies the generated HTTP host.
`Ark.Tools.Solid` supplies `IRequest<T>` and `IRequestHandler<TRequest,TResponse>`.

## 2. Define the only public contract

Create `Ping.cs`:

```csharp
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Solid;

namespace HelloMediator;

[HttpEndpoint("GET", "/api/ping")]
public sealed record Ping : IRequest<Ping, Pong>;

public sealed record Pong
{
    public required string Message { get; init; }
}
```

The contract contains no `HttpContext`, controller, Rebus bus, or gRPC server
object. `[HttpEndpoint]` is metadata. The generated endpoint will bind the
request and dispatch it to `IRequestHandler<Ping,Pong>`.

## 3. Implement the handler

Create `PingHandler.cs`:

```csharp
using Ark.Tools.Solid;

namespace HelloMediator;

public sealed class PingHandler : IRequestHandler<Ping, Pong>
{
    public async Task<Pong> ExecuteAsync(
        Ping request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await Task.CompletedTask.ConfigureAwait(false);
        return new Pong { Message = "pong" };
    }
}
```

The handler is ordinary application code. Its return value is a `Pong`
regardless of whether the caller is HTTP, gRPC, a test, or a later transport.

## 4. Compose and map the generated host

Create `Program.cs`:

```csharp
using Ark.MediatorFramework.Generated;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.MediatorFramework.MinimalApi;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using HelloMediator;

[ArkEndpointAssembly(typeof(Ping))]
public partial class HelloEndpointContext
{
}

var builder = WebApplication.CreateBuilder(args);
builder.UseArkMinimalApiStartupDiagnostics();

var container = new Container();
container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
container.Register<IRequestHandler<Ping, Pong>, PingHandler>();

builder.Services.AddArkMinimalApiHost(container);

var app = builder.Build();
app.UseArkMinimalApiHost(container);
app.UseEndpoints(endpoints =>
{
    endpoints.MapArkEndpoints<HelloEndpointContext>();
});

await app.RunAsync().ConfigureAwait(false);
```

`ArkEndpointAssembly` explicitly declares the contract assembly scanned by the
generator. The context must be a partial class so additional generated context
members can be added without modifying application contracts. Repeat the
attribute to scan more than one assembly. `AddArkMinimalApiHost` bridges
ASP.NET Core and SimpleInjector and supplies the host middleware used by
generated endpoints.

Production hosts should also add the security headers, authentication,
ProblemDetails, JSON source generation, and OpenAPI configuration described in
[Host setup and composition](host-setup-and-composition.md) and [OpenAPI](openapi.md).

## 5. Run and call it

```bash
dotnet run
curl http://localhost:5000/api/ping
```

Expected response:

```json
{ "message": "pong" }
```

The call path is:

```text
HTTP GET /api/ping
  -> generated binding
  -> IRequestHandler<Ping, Pong>
  -> PingHandler
  -> generated JSON response
```

## 6. Test the operation without HTTP

The simplest application test resolves the same handler from a scenario-owned
container and dispatches the contract:

```csharp
using AwesomeAssertions;
using Ark.Tools.Solid;

var container = new Container();
container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
container.Register<IRequestHandler<Ping, Pong>, PingHandler>();
container.Verify();

using (AsyncScopedLifestyle.BeginScope(container))
{
    var handler = container.GetInstance<IRequestHandler<Ping, Pong>>();
    var response = await handler.ExecuteAsync(new Ping()).ConfigureAwait(false);
    response.Message.Should().Be("pong");
}
```

Application tests assert business values. They do not need to assert a URL,
HTTP status, JSON casing, or generated endpoint class. Those belong in a
focused host-boundary test.

## 7. Extend this exact Ping

Do not replace `Ping`. Add one capability at a time:

1. Add a validator and authorization requirement:
   [Validation and authorization](validation-and-authorization.md).
2. Add `[GrpcMethod]` and protobuf metadata:
   [gRPC](grpc.md).
3. Create a **new** Rebus-only background contract for work that should outlive
   the HTTP request: [Rebus](rebus.md).
4. Return `IAsyncEnumerable<Pong>` for incremental output:
   [Streaming](streaming.md).
5. Configure source-generated JSON and MessagePack:
   [Serialization](serialization.md).
6. Publish versioned OpenAPI:
   [OpenAPI](openapi.md).
7. Move the same application composition behind an isolated Function host:
   [Azure Functions](azure-functions.md).
8. Add Reqnroll around direct application dispatch and keep transport tests
   separate: [Testing](testing.md).

## Compare with the sample

The sample applies the same flow with a real domain:

- public contracts:
  [`src/Ark.MediatorFramework.Sample.API`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API);
- application composition:
  [`ApplicationComposition.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/Host/ApplicationComposition.cs);
- web host:
  [`SampleStartup.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs);
- processor composition:
  [`RebusProcessorComposition.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.RebusProcessor/RebusProcessorComposition.cs);
- Reqnroll tests:
  [`Ark.MediatorFramework.Sample.Tests`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests).
