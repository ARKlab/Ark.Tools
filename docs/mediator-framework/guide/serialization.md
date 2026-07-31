# Serialization

The transport determines the wire format, not the handler. JSON is the normal
HTTP representation; MessagePack is opt-in HTTP negotiation; protobuf is the
gRPC schema. Rebus serialization is configured at the host level. Model each
format explicitly where it needs metadata.

## Serialization matrix

| Transport | Default format | Opt-in / required metadata | What the caller sends or receives |
| --- | --- | --- | --- |
| HTTP | JSON | none beyond normal JSON-serializable members | `application/json` request and response bodies |
| HTTP + MessagePack | JSON plus MessagePack negotiation | `AcceptsMessagePack = true`, `[MessagePackObject]`, configured resolver | `application/x-msgpack` body and response when negotiated |
| gRPC | protobuf | `[ProtoContract]`, stable `[ProtoMember(n)]` numbers | generated protobuf messages |
| Rebus | host-selected serializer | no transport attribute beyond `[RebusMessage]`; optional protobuf/JSON host config | serialized bus payload |

## Enable MessagePack deliberately

```csharp
[HttpEndpoint(
    "POST",
    "/api/v{version}/greetings",
    AcceptsMessagePack = true)]
[MessagePackObject(true)]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>
{
    public required string Name { get; init; }
}
```

Register an `IFormatterResolver` that can format every MessagePack contract.
The sample composes NodaTime, enum-as-string, and the standard resolver:

```csharp
var messagePackResolver = CompositeResolver.Create(
    MessagePack.NodaTime.NodatimeResolver.Instance,
    DynamicEnumAsStringResolver.Instance,
    StandardResolver.Instance);
services.AddMessagePackFormatter(messagePackResolver);
```

Expected behavior:

- JSON clients continue to work unchanged.
- Clients that send `Content-Type: application/x-msgpack` can post MessagePack.
- Clients that send `Accept: application/x-msgpack` receive MessagePack back.

## Keep three schemas compatible

Use `[ProtoContract]` and stable `[ProtoMember]` numbers for gRPC. Use
`[MessagePackObject]` and a configured resolver for MessagePack. Configure the
Ark JSON options and source-generated `System.Text.Json` metadata for JSON.
Register NodaTime protobuf surrogates before using NodaTime values over gRPC.

### Sample contract spanning all three formats

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings", AcceptsMessagePack = true)]
[GrpcMethod("CreateGreeting")]
[GrpcService("Greetings")]
[ProtoContract]
[MessagePackObject(true)]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>
{
    [ProtoMember(1)]
    public string Name { get; init; } = string.Empty;
}
```

This single contract means:

- HTTP JSON callers send `{ "name": "Ada" }`;
- HTTP MessagePack callers send the same logical payload in binary form;
- gRPC callers send the generated protobuf message;
- the handler sees the same `CreateGreetingRequest` either way.

## JSON host configuration

The sample uses Ark defaults plus a source-generated JSON context:

```csharp
services.ConfigureHttpJsonOptions(options =>
{
    var contextOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    var context = new SampleJsonSerializerContext(contextOptions);
    options.SerializerOptions.ConfigureArkDefaults();
    options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        context,
        new DefaultJsonTypeInfoResolver());
});
```

This keeps Minimal API JSON behavior explicit and fast while still allowing the
generated endpoints to serialize normal framework shapes.

## NodaTime and polymorphism

For polymorphism, define a stable discriminator and register every supported
derived type for each serializer. Do not assume JSON's type metadata works in
MessagePack or protobuf. Use a custom converter or resolver only when shared
contract metadata cannot represent the required wire shape.

For NodaTime:

- JSON uses `ConfigureArkDefaults()`.
- gRPC uses `RuntimeTypeModel.Default.AddNodaTimeSurrogates()`.
- MessagePack must use a resolver that knows the NodaTime types you expose.

Architecture rationale: [design.md](../design.md).
