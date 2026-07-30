# gRPC

`[GrpcMethod]` exposes a protobuf-compatible contract through a generated
code-first gRPC service. `[GrpcService]` selects the service that owns the
method; without an explicit method name, the contract type name is used.

## Define and map a method

```csharp
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
    public required string Message { get; init; }
}
```

```csharp
services.AddCodeFirstGrpc();
app.MapArkGrpcServicesFromAssembly<ApplicationAssemblyMarker>();
```

**Outcome:** the host exposes `Greetings/GetGreeting`, dispatches it to
`IQueryHandler<GetGreetingQuery, GreetingResponse>`, and keeps the contract
usable over HTTP or Rebus when those attributes are also present.

## Export and consume the schema

The build exports generated `.proto` files without launching the application.
Set `ArkExportProtoDir` to choose the output directory; set
`ArkExportProto=false` to disable export, and use `ArkAdditionalProto` for
hand-written shared proto files. Treat the exported schema as a release
artifact: generate clients from it, commit or publish it according to the
consumer's workflow, and review every field-number change as a wire change.

Enable server reflection only where operator tools need it. Clients send normal
gRPC authorization metadata; authentication remains a host concern.

## When to write the service yourself

Generated methods cover unary requests, generated attachment upload/download,
and `IAsyncEnumerable<T>` server streams. Use a hand-written service or a method
in the generated partial type for bidirectional conversations, custom metadata,
or an existing proto contract that must not change. See
[escape hatches](escape-hatches.md).

Architecture rationale: [design.md](../design.md).
