# Mediator Framework Azure Functions messaging design

**Status:** design baseline
**Branch:** `feature/mediator-azure-functions-bindings`
**Scope:** Azure Functions isolated worker on `net10.0`, a capability-based
transport abstraction with Azure Service Bus, Azure Storage Queue (send,
scheduled send, and QueueTrigger receive), and a first-class InMemory
transport.

## 1. Problem and outcome

The Mediator Framework currently generates Rebus wrappers for message
contracts, but receiving requires a separately deployed long-running Rebus
processor. Azure Functions should be able to receive and process the same
transport-neutral application contracts without hosting a worker process.

The result of this work is a generated Azure Functions message surface that:

- receives messages from one generated identity-queue trigger per named
  consumer participant (Service Bus PeekLock or Storage Queue QueueTrigger);
  Service Bus event subscriptions auto-forward into that identity queue;
- supports multiple contract types and JSON, MessagePack, or protobuf payloads
  in one queue;
- supports network-configured gzip/Brotli compression before transport-size
  evaluation;
- transparently offloads oversized compressed payloads to a shared DataBus;
- deserializes and dispatches through the existing SimpleInjector/Mediator
  handler model;
- preserves fail-fast, retry exhaustion, and second-level dispatch semantics;
- provides a one-way `IBus` shim for sending commands/messages, publishing
  events, and scheduled delivery;
- supports transactional SQL outbox enqueue for native `Send` and `Publish`,
  with processing hosted by a separate always-running network participant;
- demonstrates one publisher and two independent subscriber participants,
  each hosted in its own Azure Functions host, sharing contracts while using
  different handlers.

Terminology: a **participant** is a logical member of a messaging network
(producer or consumer). A **host** is the deployable process and
hosting technology that runs a participant: an Azure Functions app with
generated triggers, a Rebus-based worker, or a test/custom host running the
InMemory pump or the outbox processor.

Azure Functions is the only supported native-network hosting technology for
Processor/Consumer participants and the only host with trigger source
generation. Existing Rebus processors remain supported through their separate
wire stack and generated setup assistance. Producer-only participants are
first class: any process — a Minimal API host, a console client, another
service — can join a network as a one-way producer by composing only the
configured `IBus` from the transport-neutral messaging runtime. Storage Queue supports sending, scheduled
sending, and at-least-once receive through generated QueueTriggers; it never
supports event publishing (no topics).

## 2. Explicit boundaries

### In scope

- A new transport-neutral message attribute with one destination owner queue.
- A new transport-neutral event attribute with one canonical publisher owner.
- A shared network/bus configuration referenced by every participant.
- Assembly-level participant identity, role, subscriptions, and network
  reference.
- Generated one-trigger-per-named-participant identity queue for Service Bus
  and Storage Queue (each behind the Functions host's compile-time trigger
  selection).
- Service Bus subscriptions that forward event copies into the subscriber
  participant's identity queue.
- Runtime binding, envelope decoding, typed dispatch, scoped dependencies,
  settlement, retries, dead-lettering, and second-level dispatch.
- Service Bus `Send`, `Publish`, and delayed send.
- Storage Queue `Send`, visibility-delay scheduling, and at-least-once receive
  with `DequeueCount` and a framework-managed poison-queue DLQ.
- Producer-only participants in any process (Functions, Minimal API, client
  apps) composing only the configured `IBus`.
- Source-generated Rebus host assistance from the same network and participant
  declarations: owner routing, participant-filtered dispatch adapters, event
  subscriptions, exact retry mapping, and a runtime requirements descriptor.
- A transport-neutral messaging runtime package consumable outside Azure
  Functions; the Functions package adds trigger generation and hosting
  adapters.
- Shared DataBus claim-check storage for every transport.
- Extensible incoming/outgoing pipeline steps for context propagation and
  transport concerns.
- Concurrency-safe declaration and removal of participant-owned Service Bus
  subscriptions.
- A capability-based transport abstraction. Networks declare required
  capabilities at definition time; the concrete transport is selected at
  runtime composition.
- A first-class, shipped InMemory transport implementing every capability. It
  is a real transport usable for tests and local development, not a mock. The
  same transport-contract conformance suite runs against every transport.
- Native Mediator Framework SQL outbox enqueue for `Send` and `Publish`, plus
  an `IHostedService` processor hosted outside Azure Functions with the
  reserved network identity `outbox-processor`. The identity
  `outbox-processor` is reserved for the framework: participant declarations
  and runtime compositions using it are rejected.
- A three-participant sample demonstration.

### Out of scope

- Request/reply, replies, `SendLocal`, or any receive operation in the bus shim.
- Storage Queue subscriptions or publish fan-out emulation. Storage Queue has
  no topics; `PubSub` networks cannot run on it.
- Rebus wire interoperability. Rebus header concepts are retained, but the
  new envelope and generated host are not required to exchange messages with
  existing Rebus endpoints. Interoperability is neither required nor expected,
  so no test asserts its absence; the boundary is documentation-only.
- A long-running receive worker or outbox processor hosted inside a Functions
  app. The SQL outbox processor requires a separate always-running custom
  host. The InMemory transport's runtime message pump is a receive worker: it
  runs only in test or custom hosts, never inside a Functions app, so a
  participant composed over InMemory has no generated Azure Functions
  artifacts.
- Delayed event publication. `Publish` is immediate-only; only `Send` supports
  delayed delivery.

## 3. Contract model

The current `[RebusMessage]` attribute is transport-specific. It remains
available for Rebus compatibility, but the new feature introduces separate
transport-neutral message and event attributes.

### Messages

A message has exactly one destination queue. Any sender may send the message
to that queue. The contract metadata contains an explicit owner/destination
queue; omission is a generator error. The queue is the operational owner of
the message and is also the default route used by the Functions `IBus` shim.

Messages may be request-shaped, command-shaped, or one-way application
messages. A contract carries either `[Message]` or `[Event]`, never both;
dual attribution is a generator error. Receiving a message requires exactly
one handler registration for the queue/contract combination in a participant.
Every message and event contract is
registered once in the shared network profile. A named consumer participant
receives every registered **message** whose owner queue equals the participant
`Identity`;
there is no participant-level `ReceivedContracts` list. Events never match a
participant by publisher identity: they reach a participant only through its
explicit `Subscriptions`. Handler registration is validated separately during
startup. Scale-out instances of the same participant identity compete
normally on that one queue.

Every message/event has a stable logical contract identity. Logical names use
one global normalization, lowercase snake_case: each namespace and type
segment is lowercased and PascalCase word boundaries become underscores, so
`Books.PrintCompleted` becomes `books.print_completed`. By default the name
is the namespace-qualified CLR type name without assembly version, normalized
this way. `[Message]` and `[Event]` may override it with an explicit stable
`Name` and may declare `FormerNames` aliases for compatible CLR renames;
explicit `Name` and `FormerNames` values must already be in normalized form.
The generator rejects duplicate current names (including normalization
collisions between distinct CLR types), duplicate aliases, alias cycles,
non-normalized explicit values, and an alias that is
another contract's current name. `amf1-msg-type` writes the current logical
name; receive resolves current names and aliases through the generated registry.
The API-surface analyzer records each message/event CLR type, resolved current
name, owner, and ordinal-sorted alias set in `ArkApiSurface.txt`. Any change
produces `ARKAPI002` until the generated baseline diff is explicitly accepted.
Accepting that diff records the contract decision but does not migrate an event
topic or any existing Azure resources.

### Events

An event has exactly one canonical publisher owner and a stable contract name.
Its topic is derived as:

```text
<owner-publisher>-<contract-name>
```

The contract-name segment is the normalized logical contract identity, so
derived topics satisfy Service Bus naming rules by construction. The generator
diagnoses normalization collisions and derived topic names that exceed the
Service Bus 260-character entity limit. Because changing the current logical
name changes the topic, event renames require an explicit topology migration;
`FormerNames` supports reading old queued messages but does not implicitly
merge or rename topics.

The event is published once to the topic and is cloned by Service Bus into
subscriber queues. A participant declares its identity and the event contracts
it
subscribes to; every subscribed event must be registered in the same network.
Each subscription forwards its copy into the subscriber participant's
identity queue.
The participant has one generated queue trigger that can therefore receive
directly
addressed messages and subscribed event copies of multiple types. Two
participants
may subscribe to the same topic independently because they have distinct
subscriptions and identity queues.

The generator must reject an event declared with a missing publisher owner and
must reject event usage on a network that does not declare the `PubSub`
capability.

### Shared network/bus configuration

Participant identity is not the configuration boundary for transport behavior. A
Mediator Framework messaging network is the shared operational boundary for
native participants and the shared declaration boundary used to assist Rebus
hosts.
Every participant references exactly one network configuration. All native
participants communicating on that network use the same:

- required transport capabilities (see the capability model below);
- active serialization protocols and default protocol;
- compression algorithm and minimum compression size;
- maximum transport payload threshold;
- maximum decompressed payload size;
- DataBus offload thresholds and attachment integrity limits — the concrete
  DataBus provider/store and provider-specific lifecycle configuration are
  runtime composition decisions; all participants must compose the same
  provider,
  store, and compatible provider options as a documented deployment
  assumption;
- retry and delivery-count policy;
- resource-management and subscription-lifecycle policy; and
- connection/configuration key names, without placing secrets in attributes.

Rebus generation consumes only the portable or exactly mapped subset described
below. Native serializer/compression/DataBus/pipeline settings do not silently
become Rebus runtime settings.

The network configuration is a public transport-neutral type or declarative
attribute that can be referenced by a participant attribute. The class name of
the
type carrying the attribute is the network identity; the attribute has no
independent `Name` property. It is resolved into one immutable runtime options
object and validated once at startup. The network registers every message and
event contract participating in it. Participant attributes contain an optional
identity, event subscriptions, and participant-local incoming/outgoing steps;
they
never list received messages, select handlers, or redefine network settings.
Handler registration and step implementation dependencies remain participant
composition concerns. A participant referencing a different network profile is
a
different messaging network, even when it uses the same Azure namespace.

### Capability model and runtime transport selection

The network does not name a technology. It declares, at definition time, the
transport capabilities it requires. The concrete transport is a runtime
composition decision made for each participant by its host (for example
InMemory when testing, Azure Service Bus in production).

Capabilities are a flags-style set. `Send` and `Receive` are one foundational
capability and are implicit and always available; the optional declarable
capabilities are:

| Capability | Meaning |
| --- | --- |
| `PubSub` | Events can be published to topics and forwarded into subscriber identity queues |
| `ScheduledSend` | `Send` supports delayed delivery by duration or due time |

Each transport implementation declares the optional capabilities it supports
(`Send` and `Receive` are implicit and universal, so they do not appear here):

| Transport | PubSub | ScheduledSend |
| --- | --- | --- |
| Azure Service Bus | yes | yes |
| Azure Storage Queue | no | yes (visibility delay) |
| InMemory | yes | yes |

Storage Queue receive is at-least-once through the visibility timeout. It has
no native dead-letter queue, so the transport maps the fixed settlement
contract's dead-letter operation to a framework-managed `<queue>-poison`
companion queue, and maps the native `DequeueCount` to the delivery count.
Storage Queue has no topics, so it never supports `PubSub`.

Validation is split by binding time:

- **Compile time** validates usage against the network declaration. A consumer
  without an identity, a subscription or `[Event]` usage on a network without
  `PubSub`, and delayed-send usage on a network without `ScheduledSend` (where
  statically visible) are diagnostics.
  Compile time never checks a transport, because the transport is unknown.
- **Startup** validates the composed transport against the network
  declaration: registering a transport that does not support every declared
  capability fails startup with an explicit diagnostic.
- **Runtime** guards remain for dynamic operations: delayed `Send` and
  `Publish` throw when the capability is absent from the network declaration.

A network that requires no optional capability can therefore run on every
transport; a network requiring `PubSub` can run only on transports that support
it.

That all participants in one network use the same transport and the
same physical resources (broker namespace, DataBus store) is a runtime
operational fact and a documented deployment assumption. Each participant
validates
only its own composed transport against the shared network declaration; no
cross-participant runtime check is performed.

Rebus hosts may reference the same transport-neutral network and participant
declarations as source-generation input, but they do not join the native
Mediator Framework wire network. Rebus and Mediator Framework transports have
different headers and runtime semantics and are not wire-interoperable. Every
deployment topology must choose one receiver stack for a logical bus: either
Rebus-hosted participants or native Mediator Framework network participants.
They may reuse the same
contract registry, ownership metadata, participant identity/subscriptions, and
application handlers, but not exchange persisted messages. This boundary is
informative documentation; because interoperability is neither required nor
expected, no test asserts its absence.

### Generated Rebus host assistance

The Rebus generator consumes one network and one participant declaration from
each Rebus host assembly. It generates assistance rather than a complete Rebus
composition so applications retain control of infrastructure and
provider-specific behavior.

| Definition | Generated Rebus assistance | Remains runtime-owned |
| --- | --- | --- |
| Network message registry and message owner queues | Existing type-based owner routing for every registered message | Transport implementation, connection, credentials |
| Consumer participant identity | Participant-filtered Rebus dispatch adapters for every network message whose owner queue equals the identity; generated descriptor exposes the input queue name | Application-handler registration, input transport selection, queue creation policy, workers/concurrency |
| Participant event subscriptions | Participant-filtered event dispatch adapters plus an async generated method that calls `Subscribe<TEvent>` after the bus starts | Application-handler registration, subscription storage, broker administration |
| Producer-only participant (`Role = Producer`) | Routing and bus adapter only; no input queue, handlers, or subscriptions | One-way transport and lifecycle |
| `MaximumDeliveryCount` and `SecondLevelRetriesEnabled` | Generated options extension maps them to `ArkRetryStrategy` | Error queue name, diagnostic bounds, cooldowns, and Rebus-only options that do not alter the mapped attempt counts |
| `MaximumHandlerDuration` | Generated requirements descriptor records the value for startup validation/documentation | Transport lock duration/automatic renewal configuration |
| `RetryDelay` | No automatic mapping because Rebus retry/defer semantics differ from native Storage Queue visibility delay | Explicit Rebus retry/defer configuration |
| Serialization protocols | No automatic mapping: Rebus selects one serializer while native reads are header-driven and multi-protocol | Rebus serializer and source-generated JSON context |
| Compression | Requirements descriptor records that compression is requested; no automatic mapping until the selected Rebus algorithm/threshold is proven equivalent | Rebus compression extension and thresholds |
| DataBus | Requirements descriptor records that DataBus is required and startup validates that a Rebus DataBus callback/registration was supplied | Rebus DataBus provider, store, credentials, lifecycle, and attachment semantics |
| Participant-local pipeline steps | No mapping because Rebus pipeline implementations and ordering anchors are different | User context, telemetry, and custom Rebus pipeline configuration |
| Outbox | No inferred processor ownership; generated metadata exposes participant role only | Outbox context factory and whether this process starts the processor |
| Logging and timeouts | None | Full runtime composition |

The conceptual generated API is:

```csharp
ArkGeneratedEndpoints.ConfigureArkRebusRouting<TAssemblyMarker>(routing);
ArkGeneratedEndpoints.RegisterArkRebusDispatchAdaptersForParticipant<TAssemblyMarker>(
    container);
ArkGeneratedEndpoints.ConfigureArkRebusOptionsForParticipant<TAssemblyMarker>(options);
await ArkGeneratedEndpoints
    .SubscribeArkRebusEventsForParticipantAsync<TAssemblyMarker>(bus, cancellationToken)
    .ConfigureAwait(false);

var requirements =
    ArkGeneratedEndpoints.GetArkRebusParticipantRequirements<TAssemblyMarker>();
```

The generator sees only contracts plus network/participant declarations. It
never
discovers, references, verifies, or registers application handler
implementations. Generated Rebus dispatch adapters implement the transport
handler interface for the selected contracts, depend only on
`IRequestProcessor`/`ICommandProcessor`, and delegate application dispatch to
those processors. The developer registers every application handler in the
application container.

The final names may change, but routing and framework-owned dispatch-adapter
registration are configuration-time operations, subscriptions are an explicit
post-start async operation, and requirements are immutable generated metadata.
A generated method must never silently choose a transport, connection,
serializer, DataBus provider, subscription store, worker count, outbox
processor, or application handler.

### Glossary

| Term | Meaning |
| --- | --- |
| **Network** | The shared messaging boundary that registers all participating message/event contracts, owns native transport behavior, and supplies portable setup metadata to Rebus generation. |
| **Participant** | One logical member of a network, declared at assembly level. A participant may produce only, consume through generated Azure Functions triggers, or consume through an assisted Rebus composition. |
| **Host** | The deployable process and hosting technology that runs a participant: an Azure Functions app with generated triggers, a Rebus-based worker, or a test/custom host running the InMemory pump or the outbox processor. The host selects the concrete technology; the participant declaration never does. |
| **Identity** | The optional portable logical name of a participant. For a consumer it is also the name of its single receive queue. For publishing it grants ownership only when it equals the event's publisher owner. |
| **Queue** | A point-to-point inbox. A message's owner queue is its destination; every network message whose owner queue equals a consumer identity is received by that participant. Event publisher ownership never implies queue delivery. |
| **Subscription** | An explicit participant selection of a network event. Service Bus forwards that event into the subscriber participant's identity queue. |
| **Sender identity** | The stable identity written to `amf1-sender-identity` for the participant that invoked `Send` or `Publish`. It is routing-neutral and remains the original sender when an outbox processor later dispatches the envelope. |

Conceptual shape:

```csharp
[MessagingNetwork(
    Contracts = new[]
    {
        typeof(PrintBook),
        typeof(BookPrintCompleted)
    },
    Requires = MessagingCapabilities.PubSub
        | MessagingCapabilities.ScheduledSend,
    DefaultSerializer = SerializationProtocol.Json,
    Compression = CompressionAlgorithm.Brotli,
    CompressionMinimumSizeBytes = 4096,
    MaximumTransportPayloadBytes = 240000,
    Retry = typeof(BookRetryPolicy))]
public sealed class BookMessagingNetwork;

/// <summary>Retry/delivery policy shared by every participant on the network.</summary>
public sealed class BookRetryPolicy : IMessagingRetryPolicy
{
    /// <summary>First IFailed attempt (N). Entity/host maximum delivery
    /// is 2N so an IFailed throw can be followed by normal-T redeliveries
    /// before broker/Functions DLQ.</summary>
    public int MaximumDeliveryCount => 5;

    /// <summary>Whether delivery N invokes IFailed&lt;T&gt;. When disabled,
    /// the entity/host maximum is N; when enabled it is 2N.</summary>
    public bool SecondLevelRetriesEnabled => true;

    /// <summary>Upper bound for one handler invocation; lock renewal must
    /// cover it.</summary>
    public Duration MaximumHandlerDuration => Duration.FromMinutes(5);

    /// <summary>Delay before a non-fail-fast retry. Storage Queue maps
    /// this to host.json visibilityTimeout. Service Bus PeekLock cannot
    /// delay less than PeekLock renew, so Service Bus abandon is
    /// immediate and this value is ignored there.</summary>
    public Duration RetryDelay => Duration.FromSeconds(30);
}

// Consumer participant (hosted in Azure Functions): owns the identity queue
// and a trigger. It receives every network message whose OwnerQueue is
// "printing-functions", plus its explicit event subscriptions.
[assembly: MessagingParticipant(
    Identity = "printing-functions",
    Network = typeof(BookMessagingNetwork),
    Subscriptions = new[] { typeof(BookPrintCompleted) },
    IncomingSteps = new[] { typeof(BookUserContextIncomingStep) },
    OutgoingSteps = new[] { typeof(BookUserContextOutgoingStep) })]

// Producer-only participant (any process: Minimal API, client app,
// Functions): the identity grants event-publish ownership only; no queue, no
// trigger, no subscriptions, only a configured IBus.
[assembly: MessagingParticipant(
    Identity = "web-frontend",
    Role = MessagingParticipantRole.Producer,
    Network = typeof(BookMessagingNetwork),
    OutgoingSteps = new[] { typeof(BookUserContextOutgoingStep) })]
```

The exact `IMessagingRetryPolicy` member set is finalized by the implementation
tasks. DataBus provider options, including minimum attachment lifetime, belong
to runtime provider composition rather than the network declaration. Both
contain no secrets and are validated once at startup.

The final API may use a configuration object instead of the conceptual
attribute, but the participant reference and shared-network invariants are
fixed.
Compile-time diagnostics reject missing network references, duplicate
declarations for the same network type, usage exceeding the declared
capabilities, and participant-local overrides of shared settings. Runtime
startup
rejects divergent effective options and capability-insufficient transports.

### Participant roles

Azure Functions is the only supported native-network hosting technology for
Processor/Consumer
participants and the only host with generated triggers. Rebus consumers use the
same
participant/contract metadata through the separate generated Rebus assistance
above.
Producing is universal: any process that composes the transport-neutral
messaging runtime — a Minimal API project, a console/client application, or a
Functions app — participates as a producer through the configured `IBus`
alone.

`Identity` on `MessagingParticipant` is optional and its meaning depends on
the participant role:

- **Consumer (default role, named identity)**: the participant processes the
  queue
  named by its identity, automatically receives every network message whose
  owner queue is that identity, may declare event subscriptions, and may
  publish events whose canonical owner is that identity. Event publisher
  ownership never causes automatic event receipt. Its Functions host gets at
  most one generated trigger.
- **Producer (explicit `Role = Producer`, named identity)**: the identity
  grants event-publish ownership only. The participant owns no queue, its host
  gets no
  trigger, declares no subscriptions, and selects no handlers; declaring
  subscriptions is a compile-time diagnostic. The resource lifecycle creates
  only the topics for events owned by that identity. Typical hosts: the
  Minimal API web frontend or a client application that sends commands and
  publishes its own events.
A producer identity is optional: a producer without an identity may send
messages to their declared owner queues but cannot publish events or subscribe.
A named consumer identity always owns the foundational receive queue; a
subscription requires `PubSub`; a producer identity requires `PubSub` only when
it owns events. A network declaring no optional capability (optionally plus
`ScheduledSend`) permits producers without owned events on any transport.

### Proposed API shape

The implementation task will select final public names, XML documentation, and
API-surface entries, but the model is fixed:

```csharp
[Message(OwnerQueue = "orders")]
public sealed record RecalculateOrder : ICommand<RecalculateOrder>;

[Event(OwnerPublisher = "orders")]
public sealed record OrderRecalculated : ICommand<OrderRecalculated>;

[MessagingNetwork(
    Contracts = new[] { typeof(RecalculateOrder), typeof(OrderRecalculated) })]
public sealed class BillingMessagingNetwork;

[assembly: MessagingParticipant(
    Identity = "billing",
    Network = typeof(BillingMessagingNetwork),
    Subscriptions = new[] { typeof(OrderRecalculated) })]
```

The message/event attributes must not reference Azure SDK types or Rebus types.
The network attribute registers all message/event contracts. The assembly-level
participant attribute is participant-specific and contains an optional
identity, an optional
role, subscriptions, participant-local incoming/outgoing steps, and a reference
to the
shared network configuration. It must not contain independent serialization,
compression, DataBus, transport, or retry values — and it never names an Azure
technology: the Functions trigger binding is selected in the Functions host
project setup instead (see §6). A producer without an identity cannot declare
subscriptions. An assembly declares
at
most one `[MessagingParticipant]`: one assembly is one participant, and
duplicate declarations are a generator error.

Message owner queues and named participant identities use one portable
queue-name
contract so runtime transport selection never silently changes an address:
3–63 lowercase ASCII letters, digits, or hyphens; the first and last character
must be alphanumeric; consecutive hyphens are invalid. Event publisher
identities use the same convention. Reserved names are rejected at compile
time: the identity `outbox-processor` is reserved for the framework outbox
processor and is invalid as a participant identity, owner queue, or owner
publisher, and owner queue names ending in `-poison` are reserved for
framework-managed companion queues. The generator diagnoses violations and
derives a consumer's message set from network contracts whose owner queue
matches `MessagingParticipant.Identity` ordinally. It diagnoses unregistered
contracts, duplicate network registrations, and subscriptions to events that
are not in the network. Event topic derivation uses the normalized logical
contract name directly; the generator diagnoses normalization collisions and
derived topic names exceeding the Service Bus 260-character entity limit.

## 4. Envelope and compatibility model

The wire envelope is a byte payload plus metadata. The metadata follows Rebus
semantics where useful, but every header used by this transport is namespaced
with `amf1-*`. Rebus interoperability is not a requirement.

Required metadata:

| Header | Meaning |
| --- | --- |
| `amf1-msg-type` | Fully qualified contract type identity |
| `amf1-content-type` | Rebus-compatible content type |
| `amf1-content-encoding` | Optional standard encoding token (`gzip` or `br`) |
| `amf1-msg-id` | Stable message identifier |
| `amf1-corr-id` | Correlation identifier when present |
| `amf1-senttime` | Invariant UTC send time |
| `amf1-network` | Resolved identity of the producing network |
| `amf1-sender-identity` | Resolved identity of the participant that invoked `Send` or `Publish` |
| `amf1-payload-attachment-id` | Shared DataBus attachment ID when claim-check applies |
| `amf1-payload-attachment-length` | Expected stored byte length |
| `amf1-payload-attachment-sha256` | Expected SHA-256 digest of stored bytes |

`amf1-content-type` uses the Rebus serializer values: JSON is
`application/json;charset=utf-8`, protobuf is `application/x-protobuf`, and
MessagePack is `application/x-msgpack`. Header constants are centralized in
one package. The transport does not emit a delivery-count header.

Service Bus uses application properties for headers and the binary body for
the serialized or compressed contract. Storage Queue has no application
properties, so its body is an encoded envelope containing the binary payload
and the same header set. The Storage Queue encoder must not assume that the
payload is JSON merely because the outer envelope is encoded in a text-safe
representation. The InMemory transport stores the envelope as-is. Envelope
construction and interpretation are transport-neutral; each transport adapter
owns only the mapping between the envelope and its native message shape.

Read protocol selection is always driven by the header. Consumers do not use
the network default, compression threshold, or retry settings to interpret an
incoming message. They use `amf1-content-type`, `amf1-content-encoding`, and
the contract type header as authoritative and accept every installed supported
protocol/encoding implementation, regardless of the outbound network default.
If the header names an
unknown protocol, unsupported encoding, or unregistered contract type,
processing fails fast and the message goes directly to the transport's
dead-letter queue. It must not be retried and must not enter second-level
dispatch. A received `amf1-network` value that differs from the local network
identity means the message was produced by a different network type sharing
the receive entity. That is a fail-fast DLQ. The header does **not** detect
the same network type connected to the wrong namespace, connection, or
DataBus store: those participants write the same identity. Wrong-store
attachments
still fail on length/hash checks. Wrong-namespace same-type topology is a
documented operational assumption, not a wire check.
Senders always write the resolved network identity.
They also write `amf1-sender-identity` for both `Send` and `Publish`. A named
participant uses `MessagingParticipant.Identity`; a producer without an identity uses
the stable
host application identity required by runtime composition. The sender header
is diagnostic/audit metadata only: it does not select a queue, grant publish
ownership, or replace the original sender when a later outbox processor
dispatches the persisted envelope.

Failure details are not required envelope headers on successful or retried
messages. When the receiving transport can attach reason/description or
encode bounded metadata onto a dead-lettered copy, the adapter does so.
Service Bus uses `DeadLetterReason`/`DeadLetterErrorDescription`. Storage
Queue writes bounded metadata into the framework-moved poison body. InMemory
exposes the same metadata on its readable DLQ. A transport that cannot carry
details still dead-letters.

Contract resolution uses the generated contract registry. It must never pass an
untrusted header to unrestricted CLR type loading. Header count, key length,
value length, compressed size, and decompressed size are bounded before
application dispatch. Gzip/Brotli decompression stops when the configured
maximum decompressed payload size is exceeded.

Write protocol selection is compile-time validated. A contract-level protocol
setting and a referenced network default may both be present; conflicting
explicit values are a diagnostic. An absent contract setting uses the network
default.
The runtime serializer registry is pluggable and ships integrations for the
repository's supported JSON, MessagePack, and protobuf abstractions.

Compression is selected by network configuration. Payloads below the configured
minimum compression size are sent uncompressed. Larger payloads use the
configured gzip or Brotli encoding and set `amf1-content-encoding`; the header
is absent when the payload is uncompressed. The compressed serialized bytes are
then compared with the **effective payload limit**: the smaller of the
network-configured maximum transport payload threshold and the composed
transport's hard ceiling (§5), which already accounts for transport-specific
encoding overhead. If the bytes exceed the effective limit,
those exact compressed bytes are stored in the shared DataBus
and the envelope carries the attachment ID instead of the body. Consumers
fetch the attachment from the same shared DataBus, then decompress and
deserialize it.

All participants on one messaging network must share serialization,
compression, and
DataBus configuration for sending and attachment access. A consumer remains
header-driven and must not silently replace an incoming protocol or encoding
with the network default.

## 5. Transport abstraction, packaging, and InMemory transport

The runtime depends on one internal-facing transport contract, not on Azure
SDK types. A transport implementation provides:

- a declared `MessagingCapabilities` set;
- a hard maximum payload ceiling in bytes, already net of transport-specific
  encoding overhead, used together with the network threshold to compute the
  effective payload limit (§4, §11);
- envelope send to a named queue, with optional scheduled delivery when
  `ScheduledSend` is declared;
- envelope publish to a named topic when `PubSub` is declared;
- for `Receive`-capable transports, a receive contract with PeekLock-style
  settlement: deliver a locked envelope plus a native delivery count, and
  accept exactly one of complete, abandon, or dead-letter per delivery. This
  settlement-plus-delivery-count contract is fixed; every receive-capable
  transport must honor it;
- resource-management operations (ensure queue/topic/subscription) where the
  broker supports management, surfaced behind an optional management seam.

Three transports ship in this workstream:

1. **InMemory** — first-class and shipped, not a test double. It implements
   every capability including PeekLock-style locks with expiry, delivery
   counts, DLQ, scheduled delivery, and topic forwarding into subscriber
   queues. Tests and local development compose it exactly like a production
   transport. Its receive side is driven by a runtime message pump rather than
   generated Functions triggers. The pump is a long-running receive worker and
   therefore runs only in test or custom hosts, never inside a Functions app:
   a participant composed over InMemory has no generated Azure Functions
   artifacts. InMemory has no hard payload ceiling; the network threshold
   applies alone.
2. **Azure Service Bus** — full capability set. Its receive side is bound by
   generated Azure Functions triggers. Hard ceiling: 256 KB total standard-tier
   message size including application properties; the recommended network
   threshold is 240 000 bytes, leaving headroom for headers.
3. **Azure Storage Queue** — send, scheduled send (visibility delay), and
   at-least-once receive through a generated isolated `QueueTrigger`. Hard
   ceiling: 64 KiB per message *after* text-safe encoding, so at most 49 152
   bytes (48 KiB) of binary envelope before base64; the effective limit
   clamps the network threshold accordingly. Isolated
   QueueTrigger has no `MessageActions` equivalent: the Functions host deletes
   the message when the function returns successfully, and applies
   `host.json` `queues.visibilityTimeout` (default zero) when the function
   throws. After `queues.maxDequeueCount` failed invocations the host moves
   the message to `<queue>-poison` with no application metadata. This is
   **not** Service Bus PeekLock. The generated function binds
   `QueueMessage` (extension 5.2.0+) so `DequeueCount`, `MessageId`, and
   `PopReceipt` are available. No `PubSub`.

The effective payload limit is always `min(network threshold, transport
ceiling)`, so a network sized for Service Bus clamps safely when composed
over Storage Queue. A network intended for Storage Queue should still set its
threshold at or below 48 KiB to keep offload behavior explicit and
transport-independent. Startup warns when the configured threshold exceeds the
composed transport's ceiling.

Storage Queue settlement under QueueTrigger is therefore Functions-native
except for immediate poison:

| Dispatcher decision | Generated function |
| --- | --- |
| Complete | Return successfully. The host deletes the message. |
| Abandon (retry) | Throw. Do not catch. The host applies `visibilityTimeout` = network `RetryDelay`. |
| Immediate DLQ (fail-fast, malformed envelope, foreign `amf1-network`, missing `IFailed<T>` at delivery N) | `QueueClient`: send the envelope plus bounded failure metadata to `<queue>-poison`, `DeleteMessage` with the current pop receipt, then **return successfully** so the host does not also retry. |

AZM-11 must verify against the installed Worker Storage Queues extension that
a successful return after the SDK delete is treated as a completed
invocation (benign host delete-miss is acceptable). If the host instead
fails the invocation, AZM-11 records that evidence and switches the
immediate-DLQ path to the first behavior that does not resurrect the source
message.

Two actors can still write `<queue>-poison`: the framework SDK move
(with metadata) and the Functions host (after `maxDequeueCount`, without
metadata). The host is only the last-resort net for abandon/throw retries.
The framework send-then-delete move is not transactional. A failure between
those operations can create duplicate poison copies; this is accepted
at-least-once behavior. Poison copies preserve the original message ID so an
operator or poison consumer can deduplicate when required.
`host.json` is not generated. Required values:

- `queues.visibilityTimeout` = network `RetryDelay` (must not stay at the
  default zero);
- `queues.maxDequeueCount` = `2 × MaximumDeliveryCount` when the network
  enables second-level retries, otherwise `MaximumDeliveryCount`.

When `host.json` is supplied through `AdditionalFiles`, the generator warns
when these keys are missing or their literal values are malformed. It cannot
execute a runtime retry-policy type, so exact expected-versus-actual comparison
belongs to startup after the immutable network options are resolved. When the
generator cannot inspect `host.json`, it emits an information diagnostic.
Startup logs expected versus actual values, with an opt-in strict mode that
fails startup.

A single transport-contract conformance test suite runs against every
transport (for Azure transports, against Azurite or the Azure Service Bus
emulator, both runnable in Docker) so InMemory
semantics cannot drift from production transports.

### Packaging

The transport-neutral messaging runtime — network options, envelope, codecs,
pipeline, DataBus, transports, dispatcher, and the restricted `IBus` — lives
in a transport-neutral messaging package (working name
`Ark.Tools.MediatorFramework.Messaging`) so producer-only participants such as
a Minimal API host or a client application compose the bus without referencing
anything Functions-flavored. The Azure Functions package contains only trigger
source generation and Functions hosting adapters and depends on the messaging
package. Task documents that name
`Ark.Tools.MediatorFramework.AzureFunctions` for runtime seams are satisfied
by this messaging package; the split is finalized in the package/composition
task.

## 6. Generated Functions surface

The incremental generator consumes:

1. the participant's explicitly received message contracts, subscribed events,
   and their routing metadata;
2. the optional participant identity and role;
3. the referenced shared network configuration.

It emits:

- one stable trigger for the named consumer participant's identity queue when
  that
  participant receives messages or subscribed events — a Service Bus trigger
  or a
  Storage Queue trigger, selected by the Functions host's compile-time
  trigger selection;
- typed calls into a runtime dispatch helper;
- a deterministic subscription/resource manifest for startup;
- compile-time diagnostics for missing owners, invalid names, duplicate
  ownership, usage exceeding the declared network capabilities, protocol
  conflicts, unsupported contract shapes, and trigger-name collisions.

Azure Functions trigger attributes are compile-time facts, but the
transport-neutral participant declaration never names an Azure technology. A
Functions host running a receive-capable
participant therefore selects the trigger binding (Service
Bus or Storage Queue) through a dedicated assembly-level attribute in the
Azure Functions package,
`[assembly: MessagingFunctionsHost(MessagingFunctionsTriggerBinding.ServiceBus)]`,
consumed by the generator — rather than on `[MessagingParticipant]`. This
selection is the single source of truth
for the host's receive transport: the generated manifest records it, and
startup composition fails when the composed runtime transport does not match
the recorded binding, because the generator-side attribute and the runtime
composition are different files that can drift. Producer-only participants and
InMemory-composed participants are exempt; for them the transport remains a
pure runtime composition decision.
A participant composed with the InMemory transport cannot be hosted in Azure
Functions: no trigger artifacts are generated for it, Functions composition
rejects the InMemory receive transport, and its receive side runs through the
InMemory runtime message pump in a test or custom host against the same
dispatcher. Azure Functions end-to-end testing uses Azurite or the Azure
Service Bus emulator, both available as Docker containers.
The generated trigger is therefore an
Azure hosting adapter over the transport-neutral dispatch runtime, not the
dispatch runtime itself.

When the participant identity is absent or the participant role is Producer,
no receive
trigger or subscription manifest entry is emitted. A consumer participant whose
identity matches no registered message and that declares no subscriptions is
an empty receiver: the generator emits an information diagnostic and no
trigger.
Generated Service Bus
triggers must use PeekLock with automatic completion disabled so the runtime
settles explicitly after handler success. ReceiveAndDelete is invalid because
it cannot provide the required at-least-once processing semantics. Generated Storage Queue triggers bind `QueueMessage`, complete by returning,
abandon by throwing, and dead-letter immediately only through the SDK poison
path in §5. They cannot declare subscriptions because the transport has no
`PubSub`.

The generated method must remain thin. It receives the Azure Functions binding
type selected by the implementation after checking the exact Worker extension
API, passes the message and settlement context to the runtime helper, and
awaits the helper. No reflection-based handler dispatch or serialization logic
is emitted per contract.

## 7. Dispatch and scope semantics

The runtime pipeline is:

1. Read transport metadata and identify the contract.
2. Resolve the active serializer from the protocol header.
3. Deserialize the contract into the generated typed dispatch entry point.
4. Create one `AsyncScopedLifestyle` SimpleInjector scope for normal handling.
5. Populate message context, correlation metadata, cancellation, and claims
   context before resolving the handler.
6. Invoke the handler with `async`/`await`.
7. Complete the message only after successful handler completion.
8. On failure, apply fail-fast, inline second-level, retry, or DLQ behavior.

The runtime exposes separate incoming and outgoing async steps, modelled on
Rebus `IPipeline`, `IIncomingStep`, and `IOutgoingStep`. Steps use named
relative positions around deserialize, dispatch, serialize, transport send,
and settlement. Custom steps can add headers, establish message context, or
instrument processing without changing generated trigger methods. Built-in
user-context and OpenTelemetry steps are available but opt-in per participant.
Custom pipeline steps are registered per participant, not on the shared network
configuration (§10); the network owns only the stable stage contract and stage
identifiers.

One queue trigger represents one named participant identity.
`MessagingParticipant` selects
contracts only; it never selects handlers. The generated descriptor is
validated against SimpleInjector registrations at startup for normal message
and event handlers. The same event may be subscribed by multiple participants
because
each subscription forwards a separate Service Bus copy into its participant's
identity queue.

Let `N` be the network `MaximumDeliveryCount`. When second-level retries are
disabled, deliveries `1 .. N` run normal `T` and the receive entity/Functions
host maximum is `N`. When second-level retries are enabled, every
receive-capable transport or Functions host maximum is `2N` and the following
table applies:

`N` must be at least 1 when second-level retries are disabled and at least 2
when they are enabled, so delivery 1 always has a normal-handler attempt.

| Native delivery | Handler | On failure |
| --- | --- | --- |
| `1 .. N-1` | Normal `T` | Fail-fast → immediate DLQ. Otherwise abandon. |
| `N` | Inline `IFailed<T>` in a fresh SimpleInjector scope, or immediate DLQ if no handler is registered | `IFailed` success → complete. `IFailed` throws fail-fast → immediate DLQ. `IFailed` throws otherwise → abandon. Missing handler → immediate DLQ. |
| `N+1 .. 2N` | Normal `T` again | Same as `1 .. N-1`. Broker/Functions DLQ at `2N`. |

`IFailed<T>` is in-memory only: no failure message is persisted. It wraps
the original message, serializable exception information, and a read-only
native delivery-count snapshot. The failure handler should be idempotent
because a later delivery can run `T` again; `IFailed` itself runs once, at
delivery `N`. The network enables the second-level stage; handler presence is
not part of `MessagingParticipant` metadata. If the dispatcher cannot resolve
`IFailed<T>` at delivery `N`, that absence is a fail-fast condition and the
original envelope is dead-lettered immediately.

### Retry strategy decision and alternatives

Rebus and the repository's `ArkDefaultRetryStep` use immediate first-level
delivery retries. At exhaustion Rebus dispatches `IFailed<T>` inline. Stock
Rebus lets that handler explicitly defer the original transport message for a
later second-level attempt; the Ark retry step instead dead-letters when the
inline `IFailed<T>` handler throws. Neither behavior is a transparent delayed
first-level retry.

The selected Mediator Framework strategy remains the table above: invoke
`IFailed<T>` once at delivery `N`; if it throws a non-fail-fast exception,
abandon the locked delivery and resume normal `T` deliveries through `2N`.
This differs deliberately from Ark Rebus, but keeps native broker delivery
count as the only retry state and never persists a failure wrapper.

Rejected alternatives are:

1. **Rebus/Ark terminal second-level:** dead-letter immediately when
   `IFailed<T>` throws. This is simpler and closest to the existing Ark Rebus
   implementation, but removes the post-second-level recovery window.
2. **Explicit deferred second-level:** expose a defer operation to
   `IFailed<T>` and schedule the original envelope. This is closest to stock
   Rebus, but adds persisted retry state and transport-specific scheduling
   behavior.
3. **Delayed first-level reschedule:** complete the current delivery and
   schedule a clone. This resets native delivery count and requires a
   framework attempt header, contradicting the native-count invariant.

AZM-09 must encode the selected behavior in tests so changing strategy later is
an explicit design revision.

Abandon is transport-specific:

- **Service Bus:** `AbandonMessageAsync` is immediate. PeekLock duration
  cannot exceed five minutes and cannot be used as a configurable retry
  delay. Immediate redelivery (a retry storm on a poison `T`) is accepted.
  `RetryDelay` is ignored. Generated triggers set
  `AutoCompleteMessages = false` and bind `ServiceBusMessageActions`.
  Fail-fast DLQ uses `DeadLetterMessageAsync` with bounded reason and
  description. `maxAutoLockRenewalDuration` must cover
  `MaximumHandlerDuration`.
- **Storage Queue:** abandon is a thrown exception so the host applies
  `visibilityTimeout` = `RetryDelay`. Immediate DLQ uses the SDK poison
  path in §5.
- **InMemory:** the conformance suite implements abandon with a test clock
  delay equal to `RetryDelay` so tests are not Service Bus-shaped.

Service Bus receive bindings must use PeekLock, never ReceiveAndDelete.
Startup validation and the guide must fail clearly when configuration could
acknowledge a message before handler completion or let the lock expire
without surfacing a processing failure.

Fail-fast exceptions use the existing repository marker/mechanism where
possible.

The native delivery count is read from the locked-delivery context
(`DeliveryCount`, `DequeueCount`, or InMemory). It is never copied into or
incremented in message headers.

## 8. Resource lifecycle

Queues and topics may be provisioned by IaC. The Functions startup extension
must also be able to ensure declared resources exist, because Rebus currently
supports startup creation and this is useful for local/test deployments.

Service Bus event subscriptions are participant-managed:

- the generated manifest describes the desired topics and subscriptions;
- startup creates the named participant identity queue;
- **either** the owning publisher **or** any subscriber may `Ensure` a
  topic declared by the network (create if missing, never delete, never
  change foreign settings). This removes publisher-first deploy coupling;
- startup creates missing forwarding subscriptions to the participant identity
  queue;
- startup **deletes obsolete subscriptions owned by that participant,
  including
  in production**. Topic consumers are not rolled with dual subscription
  sets: an old subscription left in place would keep delivering events
  the participant no longer handles and spike the DLQ. There is no
  rolling-upgrade
  grace period;
- subscription names are deterministic but are not public contract API;
- operations use Service Bus management APIs and are concurrency-safe when
  multiple instances of the same or different participants start concurrently;
- deletion is restricted to subscriptions demonstrably owned by the
  participant
  identity. Queues and topics are never auto-deleted.

Storage Queue has no topics or subscriptions. Startup ensures the participant
identity queue and the framework-managed `<queue>-poison` companion queue
through the transport management seam when resource creation is enabled; both
may be IaC-precreated, ensure is idempotent, and queues are never
auto-deleted.

The implementation must account for Azure Service Bus naming and management
limitations before selecting the deterministic name. A participant may
subscribe to
multiple topics and different participants may subscribe to the same topic
safely.

Rolling topology changes are an accepted deployment risk in both directions.
Removing a subscription can race with an old participant version that still
expects
it; adding a subscription can deliver a new event to an old participant
version that
cannot process it. The framework does not attempt version-coordinated
subscription sets. Deployments must stop/drain incompatible old processors or
use versioned participant identities/contracts when zero-overlap rollout is
required.

## 9. Restricted bus shim

The Functions composition registers a restricted `IBus` implementation. It is
called a shim because it deliberately offers the familiar Rebus-like one-way
surface needed by application handlers while omitting the larger Rebus API.
That keeps the framework surface small and makes switching between a Rebus
adapter and the native network bus a composition decision. Its public
operations are one-way, and every operation accepts optional application
headers:

```csharp
Task Send<T>(
    T message,
    Dictionary<string, string>? additionalHeaders = null,
    CancellationToken cancellationToken = default);

Task Send<T>(
    T message,
    TimeSpan delay,
    Dictionary<string, string>? additionalHeaders = null,
    CancellationToken cancellationToken = default);

Task Send<T>(
    T message,
    DateTimeOffset dueTime,
    Dictionary<string, string>? additionalHeaders = null,
    CancellationToken cancellationToken = default);

Task Publish<T>(
    T @event,
    Dictionary<string, string>? additionalHeaders = null,
    CancellationToken cancellationToken = default);
```

The shim:

- validates that sent messages have an explicit owner queue;
- routes events to `<owner-publisher>-<contract-name>`;
- permits `Publish` only when the network declares `PubSub` and the current
  named participant identity equals the event's owner publisher;
- permits delayed `Send` only when the network declares `ScheduledSend`;
- selects and applies the write protocol;
- creates `amf1-*` type/correlation/message/sender-identity headers;
- accepts additional caller headers while rejecting reserved-header overrides;
- delegates delivery, scheduling, and publishing to the composed transport
  (Service Bus native scheduling, Storage Queue visibility delay, InMemory
  scheduling);
- runs the outgoing pipeline around serialization and transport send, with
  steps positioned relative to the serialize and send stages (§10);
- never exposes request/reply, replies, local send, or receive semantics.

Framework routing, serialization, DataBus, trace, and user-context headers are
reserved. Caller-supplied headers are bounded and cannot impersonate built-in
propagation steps.

The shim owns serialization and routing so a message sent by one Functions
trigger can be consumed by another generated trigger without application code
knowing the transport details.

The public `IBus` contract belongs to Mediator Framework rather than Rebus.
Rebus composition registers an adapter that proxies this restricted contract
to Rebus `IBus`; the Mediator Framework network registers its native
implementation. Application handlers depend only on the framework contract.

## 10. Pipeline and propagation

The Functions transport provides opt-in built-in incoming and outgoing steps.
The step contracts are transport-neutral and follow Rebus's continuation-based
model:

```text
IncomingStep.Process(context, next)
OutgoingStep.Process(context, next)
```

Named positions place custom steps before or after deserialization, dispatch,
serialization, send, and settlement. The built-in user-context step mirrors
the existing `ark-user-*` behavior. The built-in OpenTelemetry step propagates
W3C trace context and baggage and creates or continues an activity around
processing. Steps are registered per participant, not per network:
implementations
can carry heavy participant-only dependencies and participants/environments
may intentionally
select different step sets or ordering. The network defines the stable stage
contract only. Each participant resolves its own steps through its composition
root.

Additional headers supplied to `Send` or `Publish` flow through outgoing steps.
Reserved routing, content, compression, attachment, and identity headers
cannot be overridden; the API rejects such input explicitly.

## 11. DataBus claim-check

The runtime exposes a shared DataBus abstraction equivalent to Rebus
`IDataBus`, `DataBusAttachment`, and claim-check steps. The network declares
only transport-facing offload thresholds and integrity bounds. The concrete
provider/store and provider-specific minimum attachment lifetime are runtime
composition decisions, exactly like the transport: startup composes a
provider, and that all participants on one network compose the same provider,
store,
and compatible options is a documented deployment assumption validated per
participant, not cross-participant.

Sending serializes first, optionally compresses, then compares the resulting
bytes with the effective payload limit — the smaller of the network's
configured maximum payload threshold and the composed transport's hard ceiling
(§5). If the payload is
too large, the compressed bytes are stored in DataBus and the transport
message carries `amf1-payload-attachment-id`. Consumers transparently fetch
the attachment, verify byte length and SHA-256 metadata, decompress within the
configured output bound if required, and deserialize.

Attachments are not deleted by a consumer. Provider-specific lifecycle cleanup
owns deletion so retries, duplicate deliveries, and multiple subscribers can
all read the same attachment.

Retention is deliberately not a network-level abstraction:

1. A shared network retention value was rejected because technologies expose
   materially different lifecycle controls and deletion timing.
2. Leaving lifetime completely undocumented was rejected because a valid
   queued message could reference an expired attachment.
3. The selected model puts `MinimumAttachmentLifetime` on concrete provider
   composition. Startup validates the bounded values it knows, such as maximum
   scheduled delay and configured retry/lock windows. Operators must choose a
   larger value that also covers entity TTL, backlog, host outages, deployment
   delays, and outbox dwell time when the native SQL outbox is enlisted; the
   framework cannot prove those external bounds.

When the native SQL outbox is enlisted, the DataBus attachment is written
before the database transaction commits; a rolled-back transaction leaves an
orphaned attachment that provider lifecycle cleanup eventually removes. This
is accepted at-least-once hygiene, not an error path.

The InMemory provider implements deterministic expiry for tests. The Azure
Blob provider is implemented by AZM-07A. It writes attachments under a
dedicated container/prefix and assumes an IaC-managed Azure Storage lifecycle
rule performs retention cleanup. Runtime startup does not create or update the
account-wide lifecycle policy: Azure requires the policy to be replaced as a
whole, management-plane permissions are broader than data-plane access, shared
accounts create ownership races, and policy execution is asynchronous. The
task supplies the required lifecycle rule shape and validates Blob data-plane
access, while lifecycle provisioning remains out of scope.

## 12. Sample proof

The existing Book sample is extended with three messaging participants sharing
one contract assembly and one network profile:

1. **Publisher participant:** a producer-only participant (`Role = Producer`)
   run by a non-Functions host (for example the Minimal API web host); it owns
   the event topic, owns no queue, and has no event handler.
2. **Subscriber A:** declares the event subscription and registers handler A.
3. **Subscriber B:** declares the same event subscription independently and
   registers handler B.

The demonstration verifies one topic publication produces one delivery to each
subscriber identity queue and that handlers A and B can differ. A message flow also
demonstrates direct queue sending, typed binding, scheduled delivery, and
failure/second-level behavior.

The Book printing/background activity is runnable in two separate,
non-interoperable topology modes:

1. Rebus sender and standalone Rebus processor.
2. Mediator Framework sender and generated Azure Functions receiver.

Both modes reuse the new transport-neutral `IBus`, `IFailed<T>`, contract
ownership metadata, and application handlers. Rebus registers adapters to its
native bus and failed-message API. The Rebus producer-only (`Role = Producer`)
and Consumer participants also reference the same network/participant
declarations so generated routing,
participant-filtered dispatch adapters, subscriptions, retry options, and
requirements
assist their composition. Both backends support durable SQL outbox enqueue; the
registered bus backend selects the transport-specific producer and processor:

| Registered `IBus` backend | Outbox composition |
| --- | --- |
| Rebus adapter | Existing `Ark.Tools.Outbox.Rebus` durable outbox |
| Native Mediator Framework network bus | Native envelope producer over `Ark.Tools.Outbox`/`Ark.Tools.Outbox.SqlServer`, drained by the network outbox processor |

The existing sample `WebInterface` remains a Rebus-backed sender and keeps the
real Rebus outbox with its processor disabled. The existing
`RebusProcessor` remains Rebus-backed and keeps the real Rebus outbox with its
processor enabled. Their registrations and behavior must not be replaced by
the native implementation.

When the native bus is enlisted in `IOutboxContextCore`, `Send` and `Publish`
serialize the complete AMF envelope and destination metadata into the SQL
outbox in the same database transaction as application state. The persisted
envelope includes `amf1-sender-identity`, so later dispatch preserves the
original application participant rather than reporting the processor as the
sender.
Calls made without an enlisted outbox context send directly.

The native processor is framework-supported as an `IHostedService` registered
as a participant in the same network with the reserved, hardcoded identity
`outbox-processor`. The generator rejects any `[MessagingParticipant]`
declaration using that identity, and startup validation rejects
composition-supplied identities using it. It owns no receive queue or event
subscriptions. It
peek-locks durable outbox batches, sends their already validated raw envelopes
through the configured network transport, and commits deletion only after the
transport send succeeds. The reserved identity identifies the running
processor operationally; it does not overwrite envelope sender identity or
bypass public `Publish` ownership checks at enqueue time.

The processor must run in a separate custom always-running host, such as a
Worker Service or existing processor process. Azure Functions hosts may
enqueue SQL outbox messages but must never start the processor because polling
does not scale to zero cleanly. No Rebus receive worker or Rebus outbox
processor is started inside a Functions host.

## 13. Test strategy and release gates

- Every task leaves the repository in a runnable state: the full-solution
  build and test gates pass at the end of each task. Incomplete feature
  coverage is acceptable; broken or dispatcher-less generated code is not.
- Generator tests assert deterministic triggers, manifests, diagnostics,
  routing, protocol conflicts, and capability-usage violations.
- Envelope/serializer tests cover all three protocols, multiple types in one
  queue, missing installed codecs, malformed headers, binary payloads,
  compression, and DataBus claim-check.
- Pipeline tests cover ordering, custom header propagation, user context,
  OpenTelemetry context, cancellation, and failure behavior.
- Rebus generator tests cover Producer-only versus Consumer participant output,
  owner routing, identity-filtered contract adapters, awaited subscriptions,
  exact retry mapping, absence of handler-symbol discovery, and the
  non-generated runtime requirements boundary.
- Native outbox tests cover atomic SQL enqueue for `Send`/`Publish`, original
  sender preservation, processor locking/backoff, and rejection of processor
  hosting inside Azure Functions.
- A shared transport-contract conformance suite covers send, scheduled send,
  publish/forwarding, PeekLock settlement, delivery count, DLQ, and lock
  expiry. It runs fully against the InMemory transport and, where
  infrastructure permits, against Azure transports.
- Runtime dispatch, settlement, scope, retry, second-level, and scheduling
  tests run against the first-class InMemory transport.
- Resource lifecycle tests run concurrent participant-start and
  participant-update
  operations and verify owned obsolete subscriptions are removed.
- Storage Queue poison-alignment tests cover QueueTrigger throw-to-retry,
  `visibilityTimeout` = `RetryDelay`, the SDK immediate-poison path
  (including host delete-miss after `DeleteMessage`), generator
  `host.json` diagnostics, and the startup expected-versus-actual warning.
- Azure Blob DataBus tests cover managed identity/connection configuration,
  integrity checks, concurrent readers, and the documented IaC lifecycle-rule
  contract.
- Cross-network-type tests prove a foreign `amf1-network` identity fails
  fast to the dead-letter queue. Same-type wrong-namespace is not asserted.
- Boundary tests use the real Functions host with Azurite (Storage Queue and
  Blob) or the Azure Service Bus emulator, both runnable in Docker; absence is
  explicit, never a silent skip.
- The sample test proves three-participant publish/subscribe behavior and
  distinct handlers.
- Rebus/Mediator-Framework non-interoperability is documented, not tested:
  interoperability is neither required nor expected, so asserting its absence
  is needless.

Every implementation task uses:

```text
dotnet build Ark.Tools.slnx --configuration Debug
dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1
```

## 14. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Worker extension binding API changes | Verify exact package API in the foundation task and keep generated code thin |
| Header/property size limits | Centralize bounded metadata and failure-detail truncation |
| Network payload threshold exceeds the composed transport's hard ceiling | Effective payload limit is `min(network threshold, transport ceiling)`; startup warns on the mismatch |
| Compression changes payload bytes | Carry standard content-encoding and claim-check final compressed bytes |
| DataBus attachment deleted during retry | Use provider lifecycle cleanup, never consumer deletion |
| Custom propagation diverges between participants | Keep stable pipeline stage
  contracts, make participant-local step choices explicit, and cover each
  composition |
| Unsupported protocol in an old message | Direct fail-fast DLQ with explicit diagnostics |
| Second-level handler fails at exhaustion | Fail-fast → DLQ; otherwise abandon. Next deliveries are normal `T` until `2N` |
| Service Bus cannot delay abandon | Immediate `AbandonMessageAsync`; retry storm accepted. `RetryDelay` applies only to Storage Queue `visibilityTimeout` |
| QueueTrigger success-delete after SDK poison | AZM-11 verifies host delete-miss is benign; otherwise change only the immediate-DLQ path |
| Storage Queue poison move is non-transactional | Duplicate poison copies are accepted; preserve the original message ID and document consumer-side deduplication when required |
| Functions scale-to-zero conflicts with outbox polling | Functions enqueue only; host the reserved `outbox-processor` `IHostedService` in a separate always-running process |
| Outbox dispatch loses original publisher identity | Persist the complete validated envelope, including `amf1-sender-identity`, and dispatch through an internal raw-envelope seam |
| Rolling subscription additions/removals | Accepted deployment risk: both adding and deleting subscriptions can be incompatible with old processors; stop/drain or version identities for incompatible rollouts |
| Duplicate processing during settlement | Complete only after handler success; document at-least-once semantics |
| InMemory semantics drift from Azure transports | One shared transport-contract conformance suite runs against every transport |
| Composed transport lacks a declared capability | Startup validation fails with an explicit capability diagnostic |
| Composed transport diverges from the generated Functions trigger binding | Manifest records the trigger selection; startup fails naming both |
| Functions runtime poisons Storage Queue messages before framework exhaustion | Visible `host.json` `maxDequeueCount` contract, generator diagnostic, startup warning with strict opt-in |
| Blob attachment expires while a message remains deliverable | Provider-specific minimum lifetime plus IaC lifecycle policy; operators include TTL, backlog, outages, scheduling, and retry windows |
| Host connected to a different network type | `amf1-network` fails fast. Same-type wrong namespace is undetectable; DataBus mismatches fail on length/hash |
| InMemory receive pump started inside a Functions app | Functions composition rejects the InMemory receive transport; InMemory participants use test/custom hosts; Functions e2e testing uses Azurite or the Service Bus emulator |
| Message/event API confusion | Separate attributes, diagnostics, and routing rules |
| Rebus semantic drift | Reuse header names and failure concepts without coupling the new runtime to Rebus |
