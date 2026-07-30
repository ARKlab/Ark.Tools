# gRPC

Add `[GrpcMethod]` and `[GrpcService]` to a protobuf-compatible contract.
Build exports generated and shared `.proto` files without starting the host;
the exported files can generate clients for other applications.

```csharp
[GrpcMethod("CreateGreeting")]
[GrpcService("Greetings")]
[ProtoContract]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>
{
    [ProtoMember(1)] public string Name { get; init; } = string.Empty;
}
```

Source: [`GreetingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs).

The sample calls `MapArkGrpcServicesFromAssembly<T>()`, enables reflection, and
also maps a handwritten `DocumentsGrpcService`. Set `ArkExportProtoDir` to
choose the export directory; `ArkExportProto=false` opts out and
`ArkAdditionalProto` copies handwritten files. The host exposes reflection for
gRPCui; see the sample README's gRPCui command.

When generated service shape is insufficient, implement a method in a generated
`partial` or map a handwritten service; see [escape hatches](escape-hatches.md).
Rationale: [`design.md`](../design.md).
