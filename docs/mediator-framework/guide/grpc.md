# gRPC

`[GrpcMethod]` exposes a protobuf-compatible contract through a generated
code-first gRPC service. `[GrpcService]` selects the service that owns the
method; without an explicit method name, the contract type name is used.
gRPC is the best fit when consumers want strongly typed clients, rich status
codes, compact protobuf payloads, or streaming.

## Contract attributes

| Attribute or setting | Default | Meaning | Use it when |
| --- | --- | --- | --- |
| `[GrpcMethod()]` | Contract type name | Exposes the contract as a generated gRPC method | A contract should be callable over gRPC |
| `[GrpcMethod("GetGreeting")]` | — | Overrides the gRPC method name | The public method name must stay stable while the C# type name changes |
| `[GrpcService("Greetings")]` | Namespace / `ApiGroup` fallback | Places the method into a named generated service | Multiple methods belong in the same client-facing service |
| `[ProtoContract]` and `[ProtoMember(n)]` | none | Defines protobuf wire shape | The contract or response crosses gRPC |
| `ArkExportProto` | `true` | Enables `.proto` export after build | Clients or tests should consume the generated schema |
| `ArkExportProtoDir` | `$(MSBuildProjectDirectory)/proto` | Output directory for exported `.proto` files | You want generated schema in a specific folder |
| `ArkAdditionalProto` | empty | Additional hand-written `.proto` files copied into the export folder | Generated services import shared or manual proto definitions |

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
RuntimeTypeModel.Default.AddNodaTimeSurrogates();
services.AddCodeFirstGrpc(options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());
services.AddCodeFirstGrpcReflection();

app.UseEndpoints(endpoints =>
{
    endpoints.MapArkGrpcServicesFromAssembly<ApplicationAssemblyMarker>();
    endpoints.MapCodeFirstGrpcReflectionService().AllowAnonymous();
});
```

**Outcome:** the host exposes `Greetings/GetGreeting`, dispatches it to
`IQueryHandler<GetGreetingQuery, GreetingResponse>`, and keeps the same
contract usable over HTTP or Rebus when those attributes are also present.

## Naming and versioning rules

| Input | Generated public name |
| --- | --- |
| `[GrpcService("Greetings")]` + version 1 | `GreetingsV1` |
| `[GrpcService("Greetings")]` + version 2 | `GreetingsV2` |
| `[GrpcMethod("GetGreeting")]` | `GetGreeting` |
| `[GrpcMethod()]` on `CreateGreetingRequest` | `CreateGreetingRequest` |

If a contract is versioned, the gRPC method remains the same public method
name inside the version-specific service. The sample does this with:

- `GetGreetingQuery` — active only in v1;
- `GetGreetingV2Query` — active from v2 onward;
- both still expose `GetGreeting`, but under `GreetingsV1` and `GreetingsV2`.

## Export and consume the schema

The build exports generated `.proto` files without launching the application.
The sample sets:

```xml
<PropertyGroup>
  <ArkExportProtoDir>$(MSBuildThisFileDirectory)src/Ark.MediatorFramework.Sample.WebInterface/proto</ArkExportProtoDir>
</PropertyGroup>
<ItemGroup>
  <ArkAdditionalProto Include="$(MSBuildThisFileDirectory)src/Ark.MediatorFramework.Sample.WebInterface/proto/Documents.proto" />
</ItemGroup>
```

What each MSBuild setting does:

| Setting | Typical value | Expected result |
| --- | --- | --- |
| `ArkExportProto=true` | default | Generated `.proto` files appear after build |
| `ArkExportProto=false` | opt-out | Server still works; no export folder update happens |
| `ArkExportProtoDir=.../proto` | source-controlled folder | Consumer/test projects can reference exported schema |
| `ArkAdditionalProto Include="proto/common.proto"` | one or more items | Shared hand-written proto files are copied beside the generated ones |

For the simple contract above, generated output is equivalent to:

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

Treat the exported file as the source of truth. It can include additional
imports, NodaTime mappings, attachment messages, or versioned services beyond
this simplified illustration.

## Generate a client for production or tests

The sample keeps the generated-client project in
`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.GrpcClient/`.
Its `.csproj` is the exact pattern to copy:

```xml
<ItemGroup>
  <PackageReference Include="Grpc.Net.Client" />
  <PackageReference Include="Grpc.Tools" PrivateAssets="all" />
  <Protobuf Include="../../src/Ark.MediatorFramework.Sample.WebInterface/proto/Greetings.proto"
            GrpcServices="Client" />
</ItemGroup>
```

Consumer code:

```csharp
using var channel = GrpcChannel.ForAddress("https://api.example.test");
var client = new GreetingsV1.GreetingsV1Client(channel);
var reply = await client.GetGreetingAsync(
    new GetGreetingQuery { Id = ByteString.CopyFrom(id.ToByteArray()) },
    new Metadata { { "authorization", "Bearer " + token } }).ResponseAsync;

Console.WriteLine(reply.Message);
```

Expected output for the sample data:

```text
Hello Ada
```

The client call sends `GetGreetingQuery`, waits for one `GreetingResponse`, and
throws `RpcException` for a non-success gRPC status.

## What failures look like to callers

A denied or invalid call does not return a successful payload. It throws
`RpcException` with the mapped gRPC status:

```csharp
var action = async () => await client.GetGreetingAsync(
    new GetGreetingQuery { Id = ByteString.Empty }).ResponseAsync;

var exception = await action.Should().ThrowAsync<RpcException>();
exception.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
```

Common mappings are documented in [errors](errors.md). In practice, a client
should branch on `StatusCode` and only inspect richer details when it needs
field-level or business-rule data.

## Reflection and operator tooling

`AddCodeFirstGrpcReflection()` and `MapCodeFirstGrpcReflectionService()` are
optional. Enable them where operators or test tools need service discovery.
They do not replace exported `.proto` files for production clients.

## When to write the service yourself

Generated methods cover unary requests, generated attachment upload/download,
and `IAsyncEnumerable<T>` server streams. Use a hand-written service or a method
in the generated partial type for:

- a locked existing `.proto` contract you cannot reshape from C#;
- bidi or custom metadata workflows not covered by the generator;
- transport-specific behavior that should not leak into the shared contract.

See [escape hatches](escape-hatches.md).

Architecture rationale: [design.md](../design.md).
