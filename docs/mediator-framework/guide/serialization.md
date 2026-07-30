# Serialization

The transport determines the wire format, not the handler. JSON is the normal
HTTP representation; MessagePack is opt-in HTTP negotiation; protobuf is the
gRPC schema. Model each format explicitly where it needs metadata.

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
The generated host validates required formatters at startup and uses
`application/x-msgpack` only when that content type is sent or accepted.

**Outcome:** JSON clients continue to work unchanged; clients that request
MessagePack receive the same contract in the negotiated binary format.

## Keep three schemas compatible

Use `[ProtoContract]` and stable `[ProtoMember]` numbers for gRPC. Use
`[MessagePackObject]` and a configured resolver for MessagePack. Configure the
Ark JSON options and source-generated `System.Text.Json` metadata for JSON.
Register NodaTime protobuf surrogates before using NodaTime values over gRPC.

For polymorphism, define a stable discriminator and register every supported
derived type for each serializer. Do not assume JSON's type metadata works in
MessagePack or protobuf. Use a custom converter/resolver only when the shared
contract metadata cannot represent the required wire shape.

Architecture rationale: [design.md](../design.md).
