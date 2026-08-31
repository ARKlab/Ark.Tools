# AZM-19 — Non-JSON messaging contract validation

**Category**: azure-functions-messaging · **Priority**: pre-release
**Depends on**: AZM-17, AZM-18
**Scope**: GENERATORS + ANALYZERS + SERIALIZATION
**Design**: [Headers, payload, and serialization runtime model](../../azure-functions-messaging-design.md#headers-payload-and-serialization-runtime-model), [Envelope and compatibility model](../../azure-functions-messaging-design.md#4-envelope-and-compatibility-model)

## Problem

Participant topology determines the effective wire serializer for every message
and event, but the generator currently validates protocol compatibility without
proving that contracts have the required non-JSON shape. A MessagePack or
protobuf declaration can therefore compile and fail only when the host starts or
the first payload is processed.

Before release, the topology generator must reject non-JSON routes whose
contracts cannot satisfy their effective wire protocol. Host-specific formatter,
resolver, and parser registration remains startup validation.

## Execution map

- **Shared semantic model**: calculate each message/event's effective wire
  protocol from its processor or publisher using the same model as the runtime
  registry.
- **MessagePack validation**: require `[MessagePackObject]` on contracts whose
  effective protocol is MessagePack.
- **Protobuf validation**: require the Google.Protobuf generated contract shape
  used by `ProtobufMessagingCodec`; protobuf-net `[ProtoContract]` is not valid.
- **Topology validation**: every event subscriber must support the publisher's
  effective protocol.
- **Diagnostics**: report errors on the contract and relevant participant
  declaration with the protocol, owner, and missing contract requirement.
- **Boundary**: do not inspect DI registration, custom MessagePack resolvers, or
  protobuf parser delegate registration.

## Implementation steps

1. Resolve one effective protocol for every routed message and event from the
   existing participant/default-serializer model.
2. For a message, validate the contract shape required by the processing
   participant's effective protocol.
3. For an event, validate the contract shape required by the publishing
   participant's effective protocol.
4. Require MessagePack contracts to carry the exact MessagePack attribute used
   by the installed codec. Reject lookalike attributes.
5. Require protobuf contracts to implement the Google.Protobuf message
   interfaces needed for serialization and generated typed parsing. Do not
   accept protobuf-net attributes as evidence.
6. Keep JSON free of serializer-specific decoration requirements.
7. Validate that every event subscriber declares support for the publisher's
   effective wire protocol, independent of any additional protocols it can read.
8. Avoid requiring every contract to support every serializer listed by its
   participant; only the effective protocol for that route controls contract
   shape.
9. Emit deterministic, actionable diagnostics without loading serializer
   assemblies through reflection.
10. Reuse the validation from all messaging generators so Functions, Rebus
    assistance, runtime registries, and API-surface generation cannot accept
    divergent topologies.
11. Update the sample contracts and regenerate/inspect emitted source.

## Core code shapes

The analyzer operates only on Roslyn symbols and generated topology metadata.
It validates static contract facts:

- effective MessagePack route → `[MessagePackObject]`;
- effective protobuf route → Google.Protobuf generated message shape;
- event route → every subscriber supports the publisher's protocol.

Whether a host registered a custom MessagePack resolver or
`MessageParser<T>` delegate is a runtime composition concern and remains outside
compile-time analysis.

## Guide contribution

Update the serialization and Azure Functions guides with effective-protocol
ownership, required MessagePack and Google.Protobuf contract shapes, subscriber
compatibility, compiler diagnostics, and the boundary between contract analysis
and host startup validation.

## Sample extension

Add one MessagePack event and one Google.Protobuf message to the Book topology.
Include compile fixtures for missing decoration, the wrong protobuf model, and a
subscriber that omits the publisher protocol.

## Required test coverage

- JSON routes require no MessagePack/protobuf decoration.
- Effective MessagePack messages and events require `[MessagePackObject]`.
- Lookalike MessagePack attributes are rejected.
- Effective protobuf contracts require the Google.Protobuf generated shape.
- `[ProtoContract]` alone is rejected.
- Every event subscriber supports the publisher's effective protocol.
- Additional participant serializers do not impose unused decorations on every
  contract.
- Missing host resolver/parser registration is not reported by the analyzer and
  remains covered by startup tests.
- Diagnostics identify the contract, participant, protocol, and missing
  requirement.
- Functions, Rebus, registry, and API-surface generator fixtures make identical
  decisions.

## Outcomes

- Invalid non-JSON wire contracts fail at compilation.
- Publisher/subscriber serialization compatibility is explicit.
- Compile-time contract checks remain separate from host serializer setup.

## Acceptance

- [x] MessagePack effective routes enforce `[MessagePackObject]`.
- [x] Protobuf effective routes enforce the Google.Protobuf contract shape.
- [x] Event subscriber protocol compatibility is compile-time validated.
- [x] Host resolver and parser registration remain startup concerns.
- [x] Sample, guides, diagnostics, and generated-source inspections are updated.
- [x] The [task board](../README.md) status for AZM-19 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
