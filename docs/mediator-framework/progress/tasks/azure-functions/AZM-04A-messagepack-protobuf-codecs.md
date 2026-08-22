# AZM-04A — MessagePack and protobuf codecs

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-04
**Scope**: RUNTIME + SERIALIZATION (additional codecs)
**Design**: [Headers, payload, and serialization runtime model](../../azure-functions-messaging-design.md#headers-payload-and-serialization-runtime-model), [Envelope and compatibility model](../../azure-functions-messaging-design.md#4-envelope-and-compatibility-model)

## Problem

AZM-04 delivers the headers/payload model and the JSON codec. Participants
may declare MessagePack and protobuf in `Serializers`/`DefaultSerializer`
(AZM-02), so the remaining codecs must register into the same
content-type-driven registry without changing the header-driven read model or
any AZM-04 seam.

## Execution map

- **Runtime project**: implement the MessagePack and protobuf codecs in
  `Ark.Tools.MediatorFramework.Messaging` (or codec-specific companion
  packages if dependency isolation requires it — decide here, mirroring how
  MinimalApi already content-negotiates JSON and MessagePack).
- **Existing integrations**: reuse the repository's existing MessagePack and
  protobuf abstractions already referenced by Mediator Framework projects;
  add no serializer package without approval.
- **Codec contract**: identical to AZM-04 — generics-only, AoT-ready,
  `IBufferWriter<byte>`/`ReadOnlySequence<byte>`, no `byte[]`, no runtime
  reflection.
- **Security**: MessagePack deserializes untrusted data; apply the SEC-03
  hardening posture (untrusted-data security mode, bounded reads).
- **Testing**: pure codec and cross-protocol tests in
  `Ark.Tools.MediatorFramework.Tests`.
- **Stop condition**: no transports, no compression, no DataBus, no triggers.
  No AZM-04 seam may change shape.

## Implementation steps

1. Register the MessagePack codec under `application/x-msgpack` and the
   protobuf codec under `application/x-protobuf` in the AZM-04 registry.
2. Source MessagePack options/resolvers and protobuf type registrations the
   same way the repository's existing integrations do; contracts must be
   serializable by all protocols their owner declares.
3. Extend startup validation: the installed codecs must cover the
   participant's declared `Serializers` set; sending with an uninstalled
   owner protocol fails fast with a targeted error.
4. Verify cross-protocol reads: a participant declaring all three protocols
   reads any of them, selected purely by `amf1-content-type`.
5. Add deterministic round-trip and malformed-input diagnostics per codec.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

*MessagePack codec skeleton implementing the AZM-04 `IMessagingCodec` seam:
`MessagePackSerializer.Serialize(IBufferWriter<byte>, ...)` on write and the
`ReadOnlySequence<byte>` overload on read, with deserialization hardened by
`MessagePackSecurity.UntrustedData` (SEC-03) — the same posture
`ArkMessagePackEx` in `Ark.Tools.MediatorFramework.MinimalApi` already
applies. Options/resolvers come from the existing host-registered
`IFormatterResolver`:*

```csharp
/// <summary>MessagePack codec over the host-registered formatter resolver.</summary>
public sealed class MessagePackMessagingCodec : IMessagingCodec
{
    private readonly MessagePackSerializerOptions _serializeOptions;
    private readonly MessagePackSerializerOptions _deserializeOptions;

    /// <summary>Sources options from the same resolver the MinimalApi
    /// MessagePack content negotiation uses.</summary>
    public MessagePackMessagingCodec(IFormatterResolver resolver)
    {
        _serializeOptions = MessagePackSerializerOptions.Standard.WithResolver(resolver);
        // SEC-03: incoming payloads are untrusted data; bounded, hardened reads.
        _deserializeOptions = _serializeOptions.WithSecurity(MessagePackSecurity.UntrustedData);
    }

    /// <inheritdoc />
    public string ContentType => "application/x-msgpack";

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.MessagePack;

    /// <inheritdoc />
    public void Serialize<T>(T value, IBufferWriter<byte> writer) where T : class
    {
        MessagePackSerializer.Serialize(writer, value, _serializeOptions);
    }

    /// <inheritdoc />
    public T Deserialize<T>(in ReadOnlySequence<byte> payload) where T : class
    {
        return MessagePackSerializer.Deserialize<T>(payload, _deserializeOptions);
    }
}
```

*Protobuf codec skeleton over Google.Protobuf — the dependency
`Ark.Tools.MediatorFramework.Grpc` already references. Contracts must
implement `IMessage<T>`; writes use the Google.Protobuf
`IBufferWriter<byte>` overload, and reads use the contract's generated
static `MessageParser<T>` through a per-contract delegate slot primed at
startup — no reflection, no `Activator`, no `Type.GetType`:*

```csharp
/// <summary>Protobuf codec over Google.Protobuf generated contracts.</summary>
public sealed class ProtobufMessagingCodec : IMessagingCodec
{
    /// <inheritdoc />
    public string ContentType => "application/x-protobuf";

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.Protobuf;

    /// <inheritdoc />
    public void Serialize<T>(T value, IBufferWriter<byte> writer) where T : class
    {
        if (value is not IMessage message)
            throw new InvalidOperationException(
                $"Protobuf contract '{typeof(T)}' must implement Google.Protobuf.IMessage<T>.");

        message.WriteTo(writer); // Google.Protobuf IBufferWriter<byte> overload
    }

    /// <inheritdoc />
    public T Deserialize<T>(in ReadOnlySequence<byte> payload) where T : class
    {
        var parse = ProtobufContractRegistry<T>.Parse
            ?? throw new InvalidOperationException(
                $"Protobuf contract '{typeof(T)}' has no registered MessageParser<T>.");
        return parse(payload);
    }
}

/// <summary>Per-contract parser slot primed at startup with the contract's
/// generated static <c>MessageParser&lt;T&gt;</c>.</summary>
public static class ProtobufContractRegistry<T> where T : class
{
    /// <summary>Gets or sets the typed parse delegate for <typeparamref name="T"/>.</summary>
    public static Func<ReadOnlySequence<byte>, T>? Parse { get; set; }
}

// Startup registration, once per protobuf contract (host/startup code):
ProtobufContractRegistry<BookPrintCompleted>.Parse =
    static payload => BookPrintCompleted.Parser.ParseFrom(payload);
```

*Registration into the AZM-04 registry, keyed by the Rebus content-type
values `application/x-msgpack` / `application/x-protobuf` — the registry
seam and the header-driven read model do not change shape:*

```csharp
/// <summary>Registers the additional codecs into the AZM-04 codec registry.</summary>
public static IServiceCollection AddMessagePackMessagingCodec(
    this IServiceCollection services)
{
    services.AddSingleton<IMessagingCodec, MessagePackMessagingCodec>(); // application/x-msgpack
    return services;
}

public static IServiceCollection AddProtobufMessagingCodec(
    this IServiceCollection services)
{
    services.AddSingleton<IMessagingCodec, ProtobufMessagingCodec>();    // application/x-protobuf
    return services;
}
```

*Startup-validation extension: every protocol in the participant's declared
`Serializers` set must resolve to an installed codec through
`IMessagingCodecRegistry.IsInstalled`; sending with an uninstalled owner
protocol fails fast with a targeted error:*

```csharp
/// <summary>Fails fast when a declared serializer has no installed codec.</summary>
public static void ValidateDeclaredSerializers(
    IMessagingCodecRegistry registry,
    IReadOnlyCollection<SerializationProtocol> declaredSerializers,
    string participantIdentity)
{
    foreach (var protocol in declaredSerializers)
    {
        if (!registry.IsInstalled(protocol))
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Participant '{0}' declares serializer '{1}', but no codec is installed for it.",
                    participantIdentity,
                    protocol));
    }
}
```

## Guide contribution

Update [`guide/serialization.md`](../../../guide/serialization.md) with the
MessagePack/protobuf registration model, the declared-serializer startup
validation, and the SEC-03 untrusted-data posture for MessagePack.

## Sample extension

Extend the Book fixtures so at least one Book contract round-trips through
all three protocols and multiple protocols coexist in one logical queue.

## Required test coverage

- MessagePack and protobuf round trips with binary payloads.
- Multiple protocols in one logical queue, selected by header only.
- Uninstalled-codec sends and reads fail fast with typed diagnostics.
- Startup validation covers the declared `Serializers` set.
- Malformed MessagePack/protobuf payloads fail fast without unbounded reads.
- MessagePack hardened deserialization options are asserted (SEC-03).
- Codec APIs expose no `byte[]`-based overloads and no reflection usage.

## Outcomes

- All three protocols declared by AZM-02 have registered implementations.
- Sending one protocol and reading another installed protocol is
  deterministic and header-driven.

## Acceptance

- [x] MessagePack and protobuf codecs are registered, generics-only, and
  `IBufferWriter`/`ReadOnlySequence`-based.
- [x] Cross-protocol reads are proven; no AZM-04 seam changed shape.
- [x] Startup validates installed codecs against declared serializer sets.
- [x] MessagePack applies the SEC-03 untrusted-data posture.
- [x] The [task board](../README.md) status for AZM-04A is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
