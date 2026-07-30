# Getting started

The framework lets one application operation serve HTTP, gRPC, and Rebus without
putting transport concerns in the handler. Start with one transport, verify its
behavior, then opt the same contract into the other transports.

## Workflow

1. Reference `Ark.Tools.MediatorFramework`, plus the Minimal API, gRPC, or Rebus
   package for every transport the host exposes.
2. Put contracts and their handlers in an application assembly.
3. Register that assembly's handlers and cross-cutting decorators in
   SimpleInjector.
4. Configure the selected host services and map the generated endpoints.
5. Exercise the operation through its public HTTP or gRPC interface.

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

Map the assembly after configuring the host:

```csharp
app.MapArkEndpointsFromAssembly<ApplicationAssemblyMarker>();
app.MapArkGrpcServicesFromAssembly<ApplicationAssemblyMarker>();
```

**Outcome:** `GET /api/v1/greetings/{id}` and the `Greetings/GetGreeting`
gRPC method dispatch to the same handler. Removing either transport attribute
removes only that public interface; it does not change the business operation.

## Add asynchronous processing

Add `[RebusMessage(OwnerQueue = "greetings")]` to the same request when it is
also a message. Generated routing delivers it in a message scope and invokes the
same handler. Use this for work that can be accepted now and completed by a
worker; keep the HTTP operation separate when its response must be immediate.

## Before adding a second transport

Confirm that the contract has no `HttpContext`, gRPC, or Rebus types, has stable
protobuf member numbers when gRPC is enabled, and has explicit authorization and
error behavior. Then follow [HTTP](http-endpoints.md), [gRPC](grpc.md), or
[Rebus](rebus.md) for transport-specific configuration.

Architecture rationale: [design.md](../design.md).
