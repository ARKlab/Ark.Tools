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
| Rebus | host-selected serializer | participant declaration for generated hosts; `[RebusMessage]` for the legacy path; optional protobuf/JSON host config | Rebus-serialized bus payload |
| Native messaging | participant default, read from each envelope | `[Message]`/`[Event]`, participant serializer set, installed codecs | opaque payload plus separate `amf1-*` headers |

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
    var context = new SampleApiJsonSerializerContext(contextOptions);
    options.SerializerOptions.ConfigureArkDefaults();
    options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        context,
        new DefaultJsonTypeInfoResolver());
});
```

This keeps Minimal API JSON behavior explicit and fast while still allowing the
generated endpoints to serialize normal framework shapes.

## Mediator Framework messaging JSON

Transport-neutral messaging keeps headers separate from the transport-owned
payload. The framework uses the following headers for routing and codec
selection:

| Header | Meaning |
| --- | --- |
| `amf1-msg-type` | Normalized logical contract name |
| `amf1-content-type` | Installed codec content type |
| `amf1-network` | Resolved network identity |
| `amf1-sender-identity` | Participant that sent or published |
| `amf1-msg-id` / `amf1-corr-id` | Message and correlation identifiers |
| `amf1-senttime` | Invariant sent-time value |

`IMessagingCodec` is generic-only and writes through `IBufferWriter<byte>` or
reads from `ReadOnlySequence<byte>`; the framework does not expose a buffered
`byte[]` payload or an envelope object. JSON is registered with
`services.AddArkMessaging()` and uses host-configured `JsonSerializerOptions`,
including a shared application `JsonSerializerContext`. Validate every
declared messaging contract at startup with `MessagingJsonStartupValidation` so
a missing context fails before processing begins.

Receive is two-phase: `MessagingHeaderProcessor` bounds and validates headers,
checks the network identity, and resolves the codec from
`amf1-content-type`; the generated participant binder then deserializes the
selected contract. Content encoding and DataBus attachment headers then select
bounded decompression or claim-check retrieval before deserialization.

Receivers read the protocol from each native envelope, not from their current
default serializer. Retire a write protocol only after messages using it have
drained from queues, retries, schedules, and outboxes; keep the old codec
installed throughout that window. Startup rejects duplicate content types,
missing declared codecs, incompatible publisher/subscriber serializer sets, and
a default serializer absent from the participant's declared set.

Rebus uses different headers and its one host-selected serializer. Sharing CLR
contracts or declaration types does not make Rebus and native envelopes
compatible. Every deployed network must use one stack end to end.

### Additional messaging codecs

Register `AddMessagePackMessagingCodec()` and
`AddProtobufMessagingCodec()` alongside `AddArkMessaging()` to install
`application/x-msgpack` and `application/x-protobuf`. The MessagePack overload
accepting an `IFormatterResolver` uses the host's contract configuration; the
parameterless overload uses the standard resolver. MessagePack reads use the
`UntrustedData` security mode.

At startup, call `MessagingJsonStartupValidation.ValidateDeclaredSerializers`
for every participant. It rejects a participant whose `Serializers` declaration
contains a protocol without an installed codec, before any message is sent or
received. Protobuf contracts register their generated `MessageParser<T>` with
`ProtobufContractRegistry<T>.Parse`; no runtime reflection is used.

## NodaTime and polymorphism

For polymorphism, define a stable discriminator and register every supported
derived type for each serializer. Do not assume JSON's type metadata works in
MessagePack or protobuf. Use a custom converter or resolver only when shared
contract metadata cannot represent the required wire shape.

For NodaTime:

- JSON uses `ConfigureArkDefaults()`.
- gRPC uses `RuntimeTypeModel.Default.AddNodaTimeSurrogates()`.
- MessagePack must use a resolver that knows the NodaTime types you expose.

## Evolvable enums

Wrap a contract member in `EvolvableEnum<TEnum>` when the enum may gain members
after clients have shipped. This form defaults to an `int` backing type. For
any other enum backing type, specify it explicitly:
`EvolvableEnum<TEnum, TBacking>`. The analyzer reports a mismatch and a missing
`NOT_SET = 0` at compile time and warns when every backing value is already
occupied. `[Flags]` enums are not supported.

```csharp
public enum GreetingStatus
{
    NOT_SET = 0,
    Active = 1,
    Archived = 2,
}

public sealed record GreetingResponse
{
    public EvolvableEnum<GreetingStatus> Status { get; init; }
}

public enum CompactStatus : byte
{
    NOT_SET = 0,
    Active = 1,
}

public sealed record CompactResponse(
    EvolvableEnum<CompactStatus, byte> Status);
```

Use `Value` to switch as on the original enum. Unknown names and numbers expose
`Value == null`, so the `null` arm is the forward-compatible fallback:

```csharp
var action = response.Status.Value switch
{
    GreetingStatus.NOT_SET => "missing",
    GreetingStatus.Active => "show",
    GreetingStatus.Archived => "hide",
    null => $"unknown:{response.Status.Name ?? response.Status.ToNumber().ToString()}",
    _ => throw new UnreachableException(),
};
```

`Parse`/`TryParse` accept known names, unknown names, and in-range invariant
numbers, enabling route and query-string binding. `TypeConverter` supports the
same string conversion plus conversion from/to the exact backing type:

```csharp
var routeValue = EvolvableEnum<GreetingStatus>.Parse("Active");
var futureValue = EvolvableEnum<CompactStatus, byte>.Parse("255");
```

Per-transport wiring (see [design.md](../design.md) → *Evolvable enums* for
the full rules table):

- **JSON** (`Ark.Tools.SystemTextJson`): zero setup — `ConfigureArkDefaults()`
  already registers the converter factory. Members serialize as the symbolic
  name by default; opt into the numeric wire form per-property with
  `[JsonConverter(typeof(EvolvableEnumIntegerJsonConverterFactory))]` or by
  registering `EvolvableEnumIntegerJsonConverterFactory` directly.
- **Dapper** (`Ark.Tools.Dapper`): call
  `EvolvableEnumDapper.Register<GreetingStatus>()` once at startup (add
  `EvolvableEnumWireFormat.Number` for the integer column form).
- **protobuf-net / gRPC** (`Ark.Tools.Protobuf`): call
  `RuntimeTypeModel.Default.AddEvolvableEnumSurrogate<GreetingStatus>()` once
  at startup, per wrapped enum type (protobuf-net cannot auto-apply a
  surrogate registered on the open generic type definition). The generated
  `.proto` field is the matching `int32`, `uint32`, `int64`, or `uint64`.
- **MessagePack** (`Ark.Tools.MessagePack`): compose
  `options.WithEvolvableEnumSupport()` into the serializer options once — the
  resolver supports every wrapped enum type automatically, no per-type call
  needed.
- **Rebus**: whichever body serializer the host configures (STJ or
  protobuf-net) applies the same rules above; no separate Rebus-specific setup.

Architecture rationale: [design.md](../design.md).
