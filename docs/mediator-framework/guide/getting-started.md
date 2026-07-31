# Getting started

The framework lets one application operation serve HTTP, gRPC, and Rebus without
putting transport concerns in the handler. Start with one transport, verify its
public behavior, then opt the same contract into the other transports.

## What you need before the first endpoint

| Concern | What to add | Why |
| --- | --- | --- |
| Core contracts and handlers | `Ark.Tools.MediatorFramework` | Shared mediator abstractions and generator-facing attributes |
| HTTP host | `Ark.Tools.MediatorFramework.MinimalApi` | Generated Minimal API endpoints |
| gRPC host | `Ark.Tools.MediatorFramework.Grpc` | Generated code-first gRPC services and `.proto` export |
| Rebus worker/sender | `Ark.Tools.MediatorFramework.Rebus` | Generated Rebus wrappers and routing helpers |
| Application contracts | `Ark.Tools.Solid` interfaces such as `IQuery<T>`, `IRequest<T>`, `ICommand` | Pure transport-neutral operation model |

Keep the application assembly transport-neutral. The host assembly references
the transport packages and maps only the transports it wants to expose.

## Workflow

1. Define a contract implementing `IQuery<T>`, `IRequest<T>`, or `ICommand`.
2. Add only the transport attributes you actually want to expose.
3. Implement the handler with no ASP.NET Core, gRPC, or Rebus types.
4. Register handlers, validators, decorators, and authorization services in
   SimpleInjector.
5. Configure the selected host services and map generated endpoints.
6. Call the public HTTP or gRPC surface and verify the observable result.

## First operation

```csharp
[HttpEndpoint("GET", "/api/v{version}/greetings/{id}")]
[GrpcMethod("GetGreeting")]
[GrpcService("Greetings")]
[ProtoContract]
public sealed record GetGreetingQuery : IQuery<GreetingResponse>
{
    [ProtoMember(1)]
    public Guid Id { get; init; }
}

[ProtoContract]
public sealed record GreetingResponse
{
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    [ProtoMember(2)]
    public required string Message { get; init; }
}

public sealed class GetGreetingHandler : IQueryHandler<GetGreetingQuery, GreetingResponse>
{
    public async Task<GreetingResponse> ExecuteAsync(
        GetGreetingQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _greetings.GetAsync(query.Id, cancellationToken).ConfigureAwait(false);
    }
}
```

This contract is still a single application operation. `[HttpEndpoint]` says
the host may expose it over HTTP. `[GrpcMethod]` and `[GrpcService]` say the
host may expose it over gRPC. Removing one attribute removes only that wire.

## Register and map it

The sample splits transport-neutral registration from host wiring:

```csharp
ApplicationComposition.Register(container, useSqlStore: false);

services.AddCodeFirstGrpc();
services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapArkEndpointsFromAssembly<ApplicationAssemblyMarker>();
    endpoints.MapArkGrpcServicesFromAssembly<ApplicationAssemblyMarker>();
});
```

See [Host setup and composition](host-setup-and-composition.md) for the full
`ConfigureServices` and `Configure` workflow, decorator registration, and
middleware ordering.

## What to expect in public output

For a stored greeting with `id = 3fa85f64-5717-4562-b3fc-2c963f66afa6` and
`message = "Hello Ada"`:

### HTTP

```bash
curl --header 'Authorization: ******'       https://api.example.test/api/v1/greetings/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Hello Ada"
}
```

### gRPC

```csharp
using var channel = GrpcChannel.ForAddress("https://api.example.test");
var client = new GreetingsV1.GreetingsV1Client(channel);
var reply = await client.GetGreetingAsync(
    new GetGreetingQuery { Id = ByteString.CopyFrom(id.ToByteArray()) },
    new Metadata { { "authorization", "Bearer " + token } }).ResponseAsync;

Console.WriteLine(reply.Message);
```

Expected value written to the console:

```text
Hello Ada
```

**Outcome:** `GET /api/v1/greetings/{id}` and `Greetings/GetGreeting` dispatch
to the same handler and return the same business data through different wire
shapes.

## Add asynchronous processing

Add `[RebusMessage(OwnerQueue = "greetings")]` when the same contract must also
be dispatched as a message, or add a separate Rebus-only contract when HTTP
must return immediately and a worker completes the long-running work later.

Use a separate contract when the public synchronous result and the background
workflow are different things. The sample does this with:

- `ComposeGreetingRequest` — HTTP request returning a queued-workflow response now.
- `CompleteGreetingCompositionRequest` — Rebus message completing the work later.

## Before adding a second transport

Confirm all of the following:

- The contract has no `HttpContext`, `ServerCallContext`, or Rebus types.
- Every client-controlled field is explicit.
- Every server-owned field uses `[ServerSet]` or lives only on the response.
- Every gRPC-exposed contract has stable `[ProtoMember]` numbers.
- Authorization and validation behavior is explicit and tested.
- The operation's public status codes and error shapes are documented.

Then follow the transport-specific guides:

- [HTTP endpoints](http-endpoints.md)
- [gRPC](grpc.md)
- [Rebus](rebus.md)

Architecture rationale: [design.md](../design.md).
