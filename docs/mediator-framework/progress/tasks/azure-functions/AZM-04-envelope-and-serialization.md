# AZM-04 — Message headers, payload model, and JSON serialization runtime

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-02, AZM-03A
**Scope**: RUNTIME + SERIALIZATION (JSON only)
**Design**: [Headers, payload, and serialization runtime model](../../azure-functions-messaging-design.md#headers-payload-and-serialization-runtime-model), [Envelope and compatibility model](../../azure-functions-messaging-design.md#4-envelope-and-compatibility-model)

## Problem

One queue may contain multiple contract types and payload formats. The
metadata must carry enough information to select the contract and codec
without relying on the host's current default, stay transport-neutral, and be
processed without framework-owned buffering or runtime reflection. This task
delivers the headers/payload model and the JSON codec; MessagePack and
protobuf follow in AZM-04A over the same seams.

## Execution map

- **Runtime project**: implement header constants, the headers/payload model,
  the codec registry seam, and the JSON codec in
  `Ark.Tools.MediatorFramework.Messaging`; nothing references an Azure SDK
  type.
- **No envelope object**: headers are a plain string-to-string dictionary
  kept strictly separate from the payload throughout the framework API.
  Transports own any packaged representation (AZM-05/AZM-10/AZM-11).
- **Codec contract**: generics-only and AoT-ready —
  `Serialize<T>(T, IBufferWriter<byte>)` /
  `Deserialize<T>(ReadOnlySequence<byte>)`. The framework never allocates or
  exposes `byte[]` payload buffers; the single buffered representation is
  transport-owned.
- **Contract binding**: consume the AZM-03A generated registry and receive
  binder; expose no `Type.GetType` fallback and no `typeof(T)`-keyed receive
  lookup.
- **Serializer options**: the JSON codec resolves `JsonSerializerOptions`
  from the host's MS-DI options — the same options the MinimalApi and Azure
  Functions HTTP triggers use.
- **Testing**: pure header/codec/binder tests in
  `Ark.Tools.MediatorFramework.Tests`.
- **Runnable state**: headers, JSON codec, and the two-phase receive seam are
  complete and fully tested in isolation; nothing sends or receives yet.
- **Stop condition**: do not send, receive, compress, or access DataBus in
  this task; define seams consumed by AZM-05/AZM-07/AZM-08/AZM-09.
  MessagePack/protobuf codecs belong to AZM-04A. Transport-native mapping
  belongs to AZM-10/AZM-11. The mid-serialization compression switch belongs
  to AZM-07; the fully streamed DataBus divert is a recorded future host
  optimization.

## Implementation steps

1. Define centralized Rebus-compatible type, message, correlation, sent-time,
   delivery, protocol, and failure header constants.
2. Define `amf1-*` header constants, including Rebus-compatible
   `amf1-content-type`, optional `amf1-content-encoding`,
   `amf1-payload-attachment-id`, `amf1-network` carrying the resolved
   producer network identity, and `amf1-sender-identity` carrying the
   participant that invoked `Send` or `Publish`. `amf1-msg-type` carries the
   current logical contract name (normalized snake_case), never a CLR type
   name.
3. Model headers as a bounded string dictionary separate from the payload. Do
   not emit a delivery-count header; expose native delivery count only
   through runtime context.
4. Implement the codec registry seam keyed by Rebus content-type values
   (JSON `application/json;charset=utf-8`; `application/x-protobuf` and
   `application/x-msgpack` are registered by AZM-04A) and the JSON codec over
   the host-resolved `JsonSerializerOptions`.
5. Require a shared source-generated `JsonSerializerContext` authored in the
   contracts assembly and registered by every host of the network. Startup
   validates that every contract the participant declares is resolvable from
   the registered options and fails fast otherwise. The framework generators
   cannot emit this context (generator output is not input to the
   System.Text.Json generator), so it is user-authored and startup-validated.
6. Implement the two-phase receive seam:
   - **Header phase (non-generated)**: parse and bound headers only, classify
     fail-fast conditions, and prepare the payload source. Optional
     content-encoding and DataBus attachment headers are preserved as opaque
     values for AZM-07; do not interpret them here.
   - **Typed phase (generated, AZM-03A)**: finalize the binder signature with
     AZM-03A so the switch performs exactly `Deserialize<T>` then
     `ICommandProcessor` dispatch.
   Receive must not depend on any participant default or retry settings; an
   unknown/unsupported protocol, encoding, or contract name produces a typed
   fail-fast classification consumed by AZM-09. A received `amf1-network`
   differing from the local network identity fails fast the same way.
7. Resolve writes from the contract owner's `DefaultSerializer` through the
   AZM-03A generated registry: the processing participant's default for
   messages, the publishing participant's default for events. Sender-side
   protocol choice does not exist. Senders always write `amf1-network` and
   `amf1-sender-identity` on both `Send` and `Publish`.
8. Write the current logical contract name and resolve both current names and
   `FormerNames` aliases on receive through the generated binder.
9. Count sizes while writing/reading, never with a separate framework
   buffering pass: serialization writes through a counting
   `IBufferWriter<byte>` that throws when the network payload threshold is
   exceeded; header count/key/value bounds are enforced during header-phase
   parsing. Compressed/decompressed and attachment bounds belong to AZM-07.
10. Add deterministic round-trip and malformed-input diagnostics.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

*Centralized `amf1-*` header constants (Rebus-semantic values, no envelope
object; headers stay a plain string-to-string dictionary):*

```csharp
namespace Ark.MediatorFramework.Messaging;

public static class MessagingHeaders
{
    public const string MessageType = "amf1-msg-type";
    public const string ContentType = "amf1-content-type";
    public const string ContentEncoding = "amf1-content-encoding";
    public const string MessageId = "amf1-msg-id";
    public const string CorrelationId = "amf1-corr-id";
    public const string SentTime = "amf1-senttime";
    public const string Network = "amf1-network";
    public const string SenderIdentity = "amf1-sender-identity";
    public const string PayloadAttachmentId = "amf1-payload-attachment-id";
    public const string PayloadAttachmentLength = "amf1-payload-attachment-length";
    public const string PayloadAttachmentSha256 = "amf1-payload-attachment-sha256";
}
```

*Codec contract, registry seam, fail-fast taxonomy, and the header-phase
payload seam consumed by the AZM-03A generated binder. Generics-only,
`IBufferWriter<byte>`/`ReadOnlySequence<byte>`, no `byte[]` anywhere:*

```csharp
namespace Ark.MediatorFramework.Messaging;

public interface IMessagingCodec
{
    string ContentType { get; }                       // Rebus values, e.g. "application/json;charset=utf-8"
    SerializationProtocol Protocol { get; }
    void Serialize<T>(T value, IBufferWriter<byte> writer) where T : class;
    T Deserialize<T>(in ReadOnlySequence<byte> payload) where T : class;
}

public interface IMessagingCodecRegistry
{
    IMessagingCodec GetByContentType(string contentType);     // unknown => MessagingFailFastException
    IMessagingCodec GetByProtocol(SerializationProtocol protocol);
    bool IsInstalled(SerializationProtocol protocol);
}

public enum MessagingFailFastReason
{
    UnknownContentType, UnsupportedContentEncoding, UnknownContractName,
    ForeignNetwork, MalformedHeaders, OversizedHeaders, OversizedPayload,
    AttachmentIntegrityFailure, MissingSecondLevelHandler
}

/// <summary>Typed fail-fast classification consumed by AZM-09 dead-letter settlement.</summary>
public sealed class MessagingFailFastException : Exception
{
    /// <summary>Creates the exception with a reason and bounded, serializable detail.</summary>
    public MessagingFailFastException(MessagingFailFastReason reason, string? detail = null)
        : base(detail)
    {
        Reason = reason;
    }

    /// <summary>Gets the fail-fast classification reason.</summary>
    public MessagingFailFastReason Reason { get; }
}

// header-phase output: codec resolved, payload source prepared (attachment
// fetched / bounded decompression happen in AZM-07 behind this seam)
public interface IMessagingPayloadReader
{
    T Deserialize<T>() where T : class;
}
```

*Shared, user-authored source-generated `JsonSerializerContext` in the
contracts assembly (the framework generators cannot emit it — generator
output is not input to the System.Text.Json generator):*

```csharp
[JsonSerializable(typeof(PrintBook))]
[JsonSerializable(typeof(BookPrintCompleted))]
public sealed partial class BookContractsJsonContext : JsonSerializerContext;
```

*JSON codec skeleton: `Utf8JsonWriter` over `IBufferWriter<byte>` on write,
`Utf8JsonReader` over `ReadOnlySequence<byte>` on read, with the
host-resolved `JsonSerializerOptions` constructor-injected from MS-DI
(`IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>` for parity with the
MinimalApi/HTTP triggers; a messaging-owned accessor seam wraps this so
non-ASP.NET hosts can supply equivalent options):*

```csharp
/// <summary>JSON codec over the host-resolved <see cref="JsonSerializerOptions"/>.</summary>
public sealed class JsonMessagingCodec : IMessagingCodec
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Resolves the same options instance the MinimalApi and Azure
    /// Functions HTTP triggers use.</summary>
    public JsonMessagingCodec(IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions)
    {
        _options = jsonOptions.Value.SerializerOptions;
    }

    /// <inheritdoc />
    public string ContentType => "application/json;charset=utf-8";

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.Json;

    /// <inheritdoc />
    public void Serialize<T>(T value, IBufferWriter<byte> writer) where T : class
    {
        using var jsonWriter = new Utf8JsonWriter(writer);
        // AoT-safe: startup has validated that T resolves from the registered
        // source-generated JsonSerializerContext (see startup validation below).
        JsonSerializer.Serialize(jsonWriter, value, _options);
    }

    /// <inheritdoc />
    public T Deserialize<T>(in ReadOnlySequence<byte> payload) where T : class
    {
        var reader = new Utf8JsonReader(payload);
        return JsonSerializer.Deserialize<T>(ref reader, _options)
            ?? throw new InvalidOperationException(
                $"Payload deserialized to null for contract '{typeof(T)}'.");
    }
}
```

*Counting `IBufferWriter<byte>` skeleton: sizes are counted while writing —
never a separate measuring pass — and the writer throws the typed oversize
fail-fast at the `MessagingNetworkOptions.MaximumTransportPayloadBytes`
threshold mid-write:*

```csharp
/// <summary>Counts bytes as they are advanced and fails fast at the network
/// payload threshold.</summary>
public sealed class CountingBufferWriter : IBufferWriter<byte>
{
    private readonly IBufferWriter<byte> _inner;
    private readonly int _maximumPayloadBytes;
    private long _written;

    /// <summary>Wraps the transport-owned writer with the network threshold
    /// (<see cref="MessagingNetworkOptions.MaximumTransportPayloadBytes"/>).</summary>
    public CountingBufferWriter(IBufferWriter<byte> inner, int maximumPayloadBytes)
    {
        _inner = inner;
        _maximumPayloadBytes = maximumPayloadBytes;
    }

    /// <summary>Gets the bytes written so far.</summary>
    public long BytesWritten => _written;

    /// <inheritdoc />
    public void Advance(int count)
    {
        _written += count;
        if (_written > _maximumPayloadBytes)
            throw new MessagingFailFastException(
                MessagingFailFastReason.OversizedPayload,
                $"Payload exceeded the {_maximumPayloadBytes}-byte transport threshold.");
        _inner.Advance(count);
    }

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0) => _inner.GetMemory(sizeHint);

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0) => _inner.GetSpan(sizeHint);
}
```

*Header-phase skeleton (non-generated): parse and bound headers only,
classify typed fail-fast conditions, and hand the codec plus logical name to
the generated binder. `UnknownContractName` is raised by the binder's
`default` case (AZM-03A), not here; the local network identity comes from the
generated `NetworkIdentity` member:*

```csharp
public sealed class MessagingHeaderProcessor
{
    private const int _maxHeaderCount = 32;        // bounds enforced during parsing
    private const int _maxHeaderKeyBytes = 128;
    private const int _maxHeaderValueBytes = 4096;

    private readonly IMessagingCodecRegistry _codecs;
    private readonly string _networkIdentity;      // generated NetworkIdentity (AZM-03A)

    /// <summary>Classifies the headers of one received message.</summary>
    public (IMessagingCodec Codec, string LogicalName) Classify(
        IReadOnlyDictionary<string, string> headers)
    {
        if (headers.Count > _maxHeaderCount
            || headers.Any(static h => h.Key.Length > _maxHeaderKeyBytes
                || h.Value.Length > _maxHeaderValueBytes))
            throw new MessagingFailFastException(MessagingFailFastReason.OversizedHeaders);

        if (!headers.TryGetValue(MessagingHeaders.MessageType, out var logicalName)
            || !headers.TryGetValue(MessagingHeaders.ContentType, out var contentType)
            || !headers.TryGetValue(MessagingHeaders.Network, out var network))
            throw new MessagingFailFastException(MessagingFailFastReason.MalformedHeaders);

        if (!string.Equals(network, _networkIdentity, StringComparison.Ordinal))
            throw new MessagingFailFastException(MessagingFailFastReason.ForeignNetwork, network);

        // Unknown content type => MessagingFailFastReason.UnknownContentType.
        var codec = _codecs.GetByContentType(contentType);

        // amf1-content-encoding and amf1-payload-attachment-* are preserved
        // as opaque values for AZM-07; not interpreted in this task.
        return (codec, logicalName);
    }
}
```

*Startup-validation sketch: every declared contract must resolve from the
registered options (i.e. from the shared contracts `JsonSerializerContext`);
hosts call this once per `Processes`/`Publishes`/`Subscribes` contract:*

```csharp
public static class MessagingJsonStartupValidation
{
    /// <summary>Fails fast when a declared contract is not covered by the
    /// registered source-generated context.</summary>
    public static void ValidateContract<T>(JsonSerializerOptions options) where T : class
    {
        options.MakeReadOnly();
        if (!options.TryGetTypeInfo(typeof(T), out _))
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Contract '{0}' is not resolvable from the registered JsonSerializerOptions. "
                    + "Register the shared contracts JsonSerializerContext on this host.",
                    typeof(T)));
    }
}
```

## Guide contribution

Update [`guide/serialization.md`](../../../guide/serialization.md) and the
Azure Functions guide with `amf1-*` headers, the logical-name wire identity,
the headers/payload split (no envelope object), the host-options JSON model
and shared contracts `JsonSerializerContext` requirement, header-driven
codec reads, the two-phase receive model, and protocol retirement behavior.
Compression and claim-check guidance belongs to AZM-07.

## Sample extension

Extend the Book sample test fixtures so Book background message contracts can
be round-tripped through the headers + JSON codec and multiple types can
share one logical queue via the generated binder. Pure in-process fixtures
only; no transport exists yet. Add the shared Book contracts
`JsonSerializerContext` and its host registration.

## Required test coverage

- Multiple message types in one logical queue resolved through the generated
  binder.
- JSON round trips, including payloads containing binary data, produce bytes
  identical to serializing with the host's HTTP `JsonSerializerOptions`.
- Startup fails fast when a declared contract is not resolvable from the
  registered options/context.
- Missing, unknown, uninstalled, and conflicting protocol headers.
- Unknown contract name and malformed payload.
- Correlation/message IDs and sent time use invariant formats.
- Sender identity round-trips for both send and publish.
- Optional content-encoding and DataBus attachment headers survive
  header-phase processing without being interpreted.
- Type-confusion attempts cannot resolve contracts outside the generated
  binder; no CLR type name on the wire is honored.
- Former-name aliases deserialize to the current contract; unknown names fail
  fast.
- A foreign `amf1-network` identity produces the typed fail-fast
  classification consumed by AZM-09.
- The counting writer throws at the payload threshold mid-write; oversized
  headers fail during header-phase parsing.
- The codec API exposes no `byte[]`-based overloads.

## Caveats

- Header-driven reads must not silently fall back to any participant default.
- Rebus header compatibility does not imply Rebus endpoint interoperability.
- Do not log raw message bodies or sensitive failure details.
- Wire-format parity with HTTP holds per host through the shared options;
  cross-host parity is guaranteed by the shared contracts
  `JsonSerializerContext` requirement, validated at startup per host.

## Outcomes

- Any consumer can read every installed supported format selected by the
  message headers; JSON is installed by this task.
- The headers/payload model, codec seams, and fail-fast taxonomy are final;
  AZM-04A adds codecs and AZM-05+ add transports without reshaping them.
- Old messages are classified as fail-fast only when their codec is no longer
  installed or their contract is no longer registered; AZM-09 maps that
  classification to physical dead-letter settlement.

## Acceptance

- [ ] The JSON codec is registered, host-options-driven, generics-only, and
  `IBufferWriter`/`ReadOnlySequence`-based with no `byte[]` surface.
- [ ] A queue can contain multiple types without ambiguity via the generated
  binder; the codec registry seam accepts AZM-04A codecs unchanged.
- [ ] The headers/payload model is transport-neutral and free of Azure SDK
  types; there is no envelope object.
- [ ] Unsupported reads fail fast with bounded, serializable diagnostics.
- [ ] Startup validates contracts against the registered options/context.
- [ ] No raw payload or secret metadata is logged.
- [ ] The [task board](../README.md) status for AZM-04 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.