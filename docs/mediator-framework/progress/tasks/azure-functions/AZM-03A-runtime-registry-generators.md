# AZM-03A — Transport-neutral runtime registry generation

**Category**: azure-functions-messaging · **Priority**: foundation
**Depends on**: AZM-02
**Scope**: GENERATOR + GENERATED RUNTIME ARTIFACTS
**Design**: [Generated routing registry](../../azure-functions-messaging-design.md#generated-routing-registry), [Headers, payload, and serialization runtime model](../../azure-functions-messaging-design.md#headers-payload-and-serialization-runtime-model)

## Problem

Routing, wire-protocol, and contract-binding information is compile-time
knowledge derived from the AZM-02 network/participant declarations, but the
messaging runtime (AZM-04+) must resolve it AoT-safe with no reflection.
Today `MessagingNetworkGenerator` lives in
`Ark.Tools.MediatorFramework.AzureFunctions.Generators`, yet the declaration
model and the registry are transport-neutral: sender-only hosts (Minimal API,
console clients) must consume the registry without referencing anything
Functions-flavored.

## Execution map

- **New generator package**: create
  `src/mediator-framework/Ark.Tools.MediatorFramework.Generators`
  (transport-neutral incremental generators), packaged like the existing
  generator projects.
- **Move**: relocate `MessagingNetworkGenerator` (and the immutable
  network/participant metadata model from AZM-02) out of
  `Ark.Tools.MediatorFramework.AzureFunctions.Generators`, which keeps only
  trigger/host codegen and consumes the generated registry (AZM-10/AZM-11).
- **Shared semantic model**: the logical-name, alias, and identity resolution
  helper is source-linked and shared with
  `Ark.Tools.MediatorFramework.ApiSurface.Generators` (AZM-03) and the
  Functions/Rebus generators. One resolution implementation, no drift.
- **Marker attribute**: add a public marker attribute in
  `Ark.Tools.MediatorFramework` that excludes generated members from
  API-surface snapshots; AZM-03 makes `ApiSurfaceGenerator` honor it.
- **Tests**: generator snapshot tests in
  `tests/Ark.Tools.MediatorFramework.Tests`.
- **Stop condition**: no serialization, no header/payload runtime (AZM-04),
  no transports (AZM-05), no triggers (AZM-10/AZM-11). This task generates
  metadata-backed members only.

## Generated surface

Generated members are emitted as public partial-class members of the
attributed classes; the declaring network and participant types must be
`partial` and a diagnostic reports a non-partial (or nested/generic)
declaring type. All generated members carry the API-surface exclusion marker.
Final member names belong to this task; the shape is fixed:

- **Network partial**:
  - `GetDestinationFor<T>()` — the processing participant's identity queue
    for messages, the derived `<publisher-identity>-<contract-name>` topic
    for events;
  - `GetWireProtocolFor<T>()` — the contract owner's `DefaultSerializer`;
  - `GetLogicalNameFor<T>()` — the current wire name;
  - the resolved network identity used for `amf1-network`.
  Lookups use a `FrozenDictionary` keyed by `typeof(T)` primed in generated
  static initialization; an unknown `T` produces a typed
  contract-not-in-network exception. No `Type.GetType`, `Activator`,
  `MakeGenericType`, or delegate-map reflection anywhere.
- **Participant partial**:
  - the resolved participant identity (feeds `amf1-sender-identity`);
  - the generated receive binder: a compile-time `switch` over the current
    logical names **and `FormerNames` aliases** of the participant's
    `Processes`/`Subscribes` contracts. Each case performs exactly two
    type-parameterized actions — `Deserialize<T>` through the codec seam and
    dispatch through `ICommandProcessor.ExecuteAsync<T>` — and the default
    case returns the typed unknown-contract fail-fast classification. The
    exact binder signature (payload as `ReadOnlySequence<byte>`, deserializer
    seam, outcome type) is finalized together with AZM-04's two-phase receive
    seam; the switch shape and the two-actions-per-case rule are fixed.

## Implementation steps

1. Create the `Ark.Tools.MediatorFramework.Generators` project and move
   `MessagingNetworkGenerator` plus the AZM-02 metadata model into it;
   the Azure Functions generator package references/consumes, never
   re-derives.
2. Extract the shared name/identity resolution helper as a source-linked
   Roslyn-only component used by this package, the API-surface analyzer
   (AZM-03), and the host generators.
3. Add the API-surface exclusion marker attribute to
   `Ark.Tools.MediatorFramework` with XML documentation and an API-surface
   entry for the attribute itself.
4. Emit the network partial members with `FrozenDictionary`-backed generic
   lookups and deterministic ordering.
5. Emit the participant partial members and the receive binder switch,
   including alias cases and the typed default.
6. Diagnose non-partial, nested, or generic declaring types with a targeted
   diagnostic and record it in `AnalyzerReleases.Unshipped.md`.
7. Assert in tests that generated code contains no reflection APIs
   (`Type.GetType`, `Activator.`, `MakeGenericType`) and compiles under the
   repository's warning-as-error settings.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

*Generator pipeline shape: an `IIncrementalGenerator` (today's
`MessagingNetworkGenerator`, moved here) collecting `[MessagingNetwork]` and
`[MessagingParticipant]` classes through `ForAttributeWithMetadataName`,
combining, and emitting one deterministic `.g.cs` per attributed partial
class:*

```csharp
[Generator(LanguageNames.CSharp)]
public sealed class MessagingNetworkGenerator : IIncrementalGenerator
{
    private const string _networkAttribute = "Ark.MediatorFramework.MessagingNetworkAttribute";
    private const string _participantAttribute = "Ark.MediatorFramework.MessagingParticipantAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var networks = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                _networkAttribute,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();

        var participants = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                _participantAttribute,
                static (_, _) => true,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Collect();

        context.RegisterSourceOutput(networks.Combine(participants),
            static (productionContext, pair) =>
            {
                // Resolve the immutable network/participant model through the
                // shared source-linked name/identity helper (also used by
                // AZM-03), run the existing ARKMSG0xx topology validations,
                // then emit one .g.cs per partial network/participant class.
            });
    }
}
```

*API-surface exclusion marker attribute, declared in
`Ark.Tools.MediatorFramework` (conceptual name; AZM-03 makes
`ApiSurfaceGenerator` honor it). Every generated member carries it:*

```csharp
namespace Ark.MediatorFramework;

/// <summary>Excludes generated messaging routing members from ArkApiSurface.txt
/// snapshots; routing drift is tracked by the MESSAGE/EVENT/PARTICIPANT/NETWORK
/// snapshot lines instead.</summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class MessagingGeneratedSurfaceAttribute : Attribute;
```

*Generated participant partial for the Book sample: resolved identity plus
the receive binder — a compile-time `switch` over current logical names and
`FormerNames` aliases where each case performs exactly two typed actions,
`Deserialize<T>` through the AZM-04 payload seam (`IMessagingPayloadReader`)
then `ICommandProcessor.ExecuteAsync<T>`:*

```csharp
// <auto-generated/>
public sealed partial class PrintingParticipant
{
    /// <summary>Resolved participant identity; feeds amf1-sender-identity.</summary>
    public static string Identity => "printing";

    /// <summary>Generated receive binder: switches over the wire names of Processes/Subscribes.</summary>
    public static async Task DispatchAsync(
        string logicalName,
        IMessagingPayloadReader payload,
        ICommandProcessor processor,
        CancellationToken ctk)
    {
        switch (logicalName)
        {
            case "books.print_book":
                await processor.ExecuteAsync<PrintBook>(payload.Deserialize<PrintBook>(), ctk).ConfigureAwait(false);
                break;
            case "books.book_print_completed":
            case "books.print_finished": // FormerNames alias
                await processor.ExecuteAsync<BookPrintCompleted>(payload.Deserialize<BookPrintCompleted>(), ctk).ConfigureAwait(false);
                break;
            default:
                throw new MessagingFailFastException(MessagingFailFastReason.UnknownContractName, logicalName);
        }
    }
}
```

*Generated network partial for the Book sample: `FrozenDictionary`-backed
generic lookups keyed by `typeof(T)`, primed in generated static
initialization — no `Type.GetType`, `Activator`, or `MakeGenericType`:*

```csharp
// <auto-generated/>
public sealed partial class BookMessagingNetwork
{
    public static string NetworkIdentity => "Book.Topology.BookMessagingNetwork";

    private static readonly FrozenDictionary<Type, string> _destinations =
        new Dictionary<Type, string>
        {
            [typeof(PrintBook)] = "printing",                                  // processor identity queue
            [typeof(BookPrintCompleted)] = "web-frontend-books.book_print_completed", // <publisher-identity>-<contract-name> topic
        }.ToFrozenDictionary();

    public static string GetDestinationFor<T>() where T : class
        => _destinations.TryGetValue(typeof(T), out var d)
            ? d
            : throw new InvalidOperationException($"Contract '{typeof(T)}' is not declared by any member of this network.");

    public static SerializationProtocol GetWireProtocolFor<T>() where T : class { /* FrozenDictionary, same pattern */ }
    public static string GetLogicalNameFor<T>() where T : class { /* FrozenDictionary, same pattern */ }
}
```

*Non-partial declaring type diagnostic, following the generator's existing
`_rule` helper (the concrete id is the next free `ARKMSG0xx`, recorded in
`AnalyzerReleases.Unshipped.md`):*

```csharp
private static readonly DiagnosticDescriptor _nonPartialDeclaringType = _rule(
    "ARKMSG0xx", "Messaging declaring type must be partial",
    "Type '{0}' is marked with [{1}] but is not a non-nested, non-generic partial class, "
    + "so its routing members cannot be generated",
    DiagnosticSeverity.Error);
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md): the
network/participant classes must be declared `partial`, the generated routing
members are the only routing source for hosts and transports, and generated
members are excluded from `ArkApiSurface.txt` because the
`MESSAGE`/`EVENT`/`PARTICIPANT`/`NETWORK` snapshot lines already track the
drift.

## Sample extension

Mark the Book sample network/participant declarations `partial`, build, and
inspect the emitted `.g.cs` under
`samples/Ark.MediatorFramework.Sample/src/*/obj/Debug/net10.0/generated/` to
verify the generated members are correct and compilable.

## Required test coverage

- Destination resolution: message → processor identity queue, event →
  `<publisher-identity>-<contract-name>` topic.
- Wire-protocol resolution matches the owner's `DefaultSerializer`.
- Unknown `T` produces the typed contract-not-in-network failure.
- Binder switch covers every `Processes`/`Subscribes` contract, every
  `FormerNames` alias, and the typed unknown-name default.
- Non-partial, nested, and generic declaring types are diagnosed.
- Generated members carry the exclusion marker; API-surface output is
  unchanged by their presence (coordinated with AZM-03).
- Generated code contains no runtime-reflection API usage.
- Moving `MessagingNetworkGenerator` preserves its existing outputs
  byte-for-byte.
- Repeated compilations are deterministic.

## Outcomes

- Hosts and transports resolve routing, protocol, and contract binding
  through generated, AoT-clean members — never through reflection or runtime
  discovery.
- Transport-neutral codegen no longer lives in the Azure Functions generator
  package, so sender-only hosts stay Functions-free.

## Acceptance

- [x] `Ark.Tools.MediatorFramework.Generators` exists, owns the moved
  generator, and emits the network/participant partial members.
- [x] Generic lookups are `FrozenDictionary`-backed and reflection-free; the
  receive binder is a compile-time exhaustive switch including aliases.
- [x] Non-partial declaring types are diagnosed.
- [x] Generated members are excluded from API-surface snapshots via the
  marker attribute.
- [x] The [task board](../README.md) status for AZM-03A is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
