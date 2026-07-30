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

| MSBuild setting | Default | Meaning |
| --- | --- | --- |
| `ArkExportProtoDir` | package-defined output | Directory receiving generated `.proto` files. Use a source-controlled directory when another project generates clients from the schema. |
| `ArkExportProto` | `true` | Set to `false` to skip schema export. Generated server endpoints still work. |
| `ArkAdditionalProto` | empty item list | Additional hand-written proto files available during export, for shared messages or a hand-written service. |

```xml
<PropertyGroup>
  <ArkExportProtoDir>$(MSBuildProjectDirectory)/proto</ArkExportProtoDir>
</PropertyGroup>
<ItemGroup>
  <ArkAdditionalProto Include="proto/common.proto" />
</ItemGroup>
```

For the contracts above, generated output is equivalent to:

```proto
syntax = "proto3";
package greetings.v1;

service GreetingsV1 {
  rpc GetGreeting(GetGreetingQuery) returns (GreetingResponse);
}

message GetGreetingQuery {
  bytes id = 1;
}

message GreetingResponse {
  string message = 1;
}
```

The actual namespace, message encoding, imported well-known files, and service
suffix follow the exported contract. Treat the exported file—not this
illustration—as the source of truth. The sample's generated schemas are written
to `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/proto`
when it builds; its `GrpcClient` test project shows the exact client setup to
copy.

Generate a strongly typed C# client in a consumer or test project:

```xml
<ItemGroup>
  <PackageReference Include="Grpc.Net.Client" />
  <PackageReference Include="Grpc.Tools" PrivateAssets="all" />
  <Protobuf Include="../MyApplication/proto/Greetings.proto"
            GrpcServices="Client" />
</ItemGroup>
```

```csharp
using var channel = GrpcChannel.ForAddress("https://api.example.test");
var client = new GreetingsV1.GreetingsV1Client(channel);
var reply = await client.GetGreetingAsync(
    new GetGreetingQuery { Id = ByteString.CopyFrom(id.ToByteArray()) });

Console.WriteLine(reply.Message);
```

The client call sends `GetGreetingQuery`, waits for one `GreetingResponse`, and
throws `RpcException` for a non-success gRPC status. Pin, publish, or commit
the exported schema according to the consumer workflow, and review every
field-number change as a wire change.

Enable server reflection only where operator tools need it. Clients send normal
gRPC authorization metadata; authentication remains a host concern.

## When to write the service yourself

Generated methods cover unary requests, generated attachment upload/download,
and `IAsyncEnumerable<T>` server streams. Use a hand-written service or a method
in the generated partial type for bidirectional conversations, custom metadata,
or an existing proto contract that must not change. See
[escape hatches](escape-hatches.md).

Architecture rationale: [design.md](../design.md).
