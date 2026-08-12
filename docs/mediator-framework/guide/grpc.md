# gRPC

Add gRPC when consumers need typed clients, protobuf payloads, rich status
codes, or server streaming. The application contract remains the source of
truth; the framework generates the code-first service.

## 1. Opt a public contract into gRPC

Start from the `Ping` contract in [Getting started](getting-started.md):

```csharp
[GrpcMethod("Ping")]
[GrpcService("Health")]
[ProtoContract]
public sealed record Ping : IRequest<Ping, Pong>
{
    [ProtoMember(1)]
    public string Message { get; init; } = string.Empty;
}

[ProtoContract]
public sealed record Pong
{
    [ProtoMember(1)]
    public required string Message { get; init; }
}
```

Every protobuf member needs a stable number. Never reuse a number after a client
has shipped. Add a new number or introduce a new API version.

## 2. Register the gRPC runtime

```csharp
RuntimeTypeModel.Default.AddNodaTimeSurrogates();
services.AddCodeFirstGrpc(options =>
    options.Interceptors.Add<ArkGrpcErrorInterceptor>());
services.AddCodeFirstGrpcReflection();
```

Map the generated service next to the generated HTTP endpoints:

```csharp
endpoints.MapArkGrpcServicesFromAssembly<Ping>();
endpoints.MapCodeFirstGrpcReflectionService().AllowAnonymous();
```

The generated method dispatches to `IRequestHandler<Ping,Pong>`. No separate
gRPC handler is needed.

## 3. Export the schema

Set the export directory in the host's build properties:

```xml
<PropertyGroup>
  <ArkExportProtoDir>
    $(MSBuildThisFileDirectory)proto
  </ArkExportProtoDir>
</PropertyGroup>
```

Build without running the host. The generator emits the service and `.proto`
files. `ArkAdditionalProto` copies hand-written shared schemas beside generated
files:

```xml
<ItemGroup>
  <ArkAdditionalProto Include="$(MSBuildThisFileDirectory)proto/common.proto" />
</ItemGroup>
```

Treat the exported schema as a reviewable artifact. The sample stores its
generated output in
[`WebInterface/proto/`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/proto).

## 4. Generate a client

```xml
<ItemGroup>
  <PackageReference Include="Grpc.Net.Client" />
  <PackageReference Include="Grpc.Tools" PrivateAssets="all" />
  <Protobuf Include="../../src/Ark.MediatorFramework.Sample.WebInterface/proto/Greetings.proto"
            GrpcServices="Client" />
</ItemGroup>
```

```csharp
using Grpc.Net.Client;

using var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = new GreetingsV1.GreetingsV1Client(channel);
var response = await client.GetGreetingAsync(request).ResponseAsync
    .ConfigureAwait(false);
```

The sample keeps this client project under
[`test/Ark.MediatorFramework.Sample.GrpcClient`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.GrpcClient).
It is used by transport tests, not by application scenarios.

## 5. Handle errors

Generated handlers map application failures through the gRPC error interceptor:

| Application outcome | gRPC status |
| --- | --- |
| missing/invalid bearer token | `Unauthenticated` |
| missing required scope | `PermissionDenied` |
| validation failure | `InvalidArgument` |
| missing entity | `NotFound` |
| stale ETag | `FailedPrecondition` |

Assert `RpcException.StatusCode` in a gRPC boundary test. Assert typed
application exceptions in a direct-dispatch test.

## 6. Add a server stream

Return `IAsyncEnumerable<T>` from the query contract:

```csharp
[GrpcMethod("Watch")]
[GrpcService("Health")]
public sealed record WatchPing : IQuery<WatchPing, IAsyncEnumerable<Pong>>
{
    [ProtoMember(1)]
    public int Count { get; init; }
}
```

The handler must propagate cancellation through the iterator. See the complete
HTTP and gRPC examples in [Streaming](streaming.md).

## When to write a gRPC service yourself

Use a handwritten service only for a locked external `.proto`, bidirectional
streaming, custom metadata, or a transport behavior that must not enter the
shared contract. See [Escape hatches](escape-hatches.md).
