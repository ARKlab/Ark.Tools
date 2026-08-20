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

- receives messages from one generated identity-queue trigger for the single
  participant bound to each Functions app (Service Bus PeekLock or Storage
  Queue QueueTrigger);
  Service Bus event subscriptions auto-forward into that identity queue;
- supports multiple contract types and JSON, MessagePack, or protobuf payloads
  in one queue;
- supports participant-configured gzip/Brotli compression before
  transport-size evaluation;
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
(producer, consumer, or sender-only). A **host** is the deployable process and
hosting technology that runs a participant: an Azure Functions app with
generated triggers, a Rebus-based worker, or a test/custom host running the
InMemory pump or the outbox processor.

Azure Functions is the only supported native-network hosting technology for
Processor/Consumer participants and the only host with trigger source
generation. Existing Rebus processors remain supported through their separate
wire stack and generated setup assistance. Sender-only and publisher-only
participants are
first class: any process — a Minimal API host, a console client, another
service — can join a network as a one-way sender by composing only the
configured `IBus` from the transport-neutral messaging runtime. Storage Queue supports sending, scheduled
sending, and at-least-once receive through generated QueueTriggers; it never
supports event publishing (no topics).

## 2. Explicit boundaries

### In scope

- Transport-neutral message and event attributes carrying only the contract
  kind and its logical identity; contracts are owner-free.
- Participant declarations — attributed classes in a shared contracts/topology
  assembly — stating how the participant joins the network: which messages it
  processes, which events it publishes, which events it subscribes to, and
  which serializations it supports with which default.
- A shared network declaration listing its member participants, the required
  transport capabilities, and the shared payload/DataBus thresholds, resource
  lifecycle, and connection key names.
- Assembly-level host bindings that attach a host project to a participant
  declaration, adding host-local pipeline steps and, for Azure Functions, the
  trigger binding selection.
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
- Sender-only and publisher participants in any process (Functions, Minimal
  API, client apps) composing only the configured `IBus`.
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

A message has exactly one destination queue: the identity queue of the single
participant that declares it processes the message. The contract itself is
owner-free — `[Message]` carries only the contract kind and logical identity —
so ownership is established solely by the participant declaration, never
duplicated on the contract. Any participant may send the message; the bus
routes it to the processing participant's identity queue through the generated
registry.

Messages may be request-shaped, command-shaped, or one-way application
messages. A contract carries either `[Message]` or `[Event]`, never both;
dual attribution is a generator error. Exactly one member of a network may
declare a given message in `Processes`; the generator rejects multiple
processors and reports contracts declared by no member as unwired
(informational — a contract in development not yet joined to a network).
Receiving a message requires exactly one handler registration
for the contract in the processing participant's host composition, validated
at startup. Scale-out instances of the same participant identity compete
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
name, and ordinal-sorted alias set in `ArkApiSurface.txt`, plus separate
`PARTICIPANT` and `NETWORK` lines recording each participant's identity,
network, processes/publishes/subscribes sets and serializations, and each
network's member list and capability flags. Any change
produces `ARKAPI002` until the generated baseline diff is explicitly accepted.
Accepting that diff records the contract decision but does not migrate an event
topic or any existing Azure resources.

### Events

An event has exactly one canonical publisher: the single participant that
declares it in `Publishes`. Exactly one member of a network may publish a
given event; the generator rejects multiple publishers and unwired events.
Its topic is derived as:

```text
<publisher-identity>-<contract-name>
```

The contract-name segment is the normalized logical contract identity, so
derived topics satisfy Service Bus naming rules by construction. The generator
diagnoses normalization collisions and derived topic names that exceed the
Service Bus 260-character entity limit. Because changing the publisher
identity or the current logical
name changes the topic, event renames and ownership moves require an explicit
topology migration;
`FormerNames` supports reading old queued messages but does not implicitly
merge or rename topics.

The event is published once to the topic and is cloned by Service Bus into
subscriber queues. A participant declares the event contracts it
subscribes to; every subscribed event must be published by a member of the
same network — an unsatisfiable subscription is a generator error.
Each subscription forwards its copy into the subscriber participant's
identity queue.
The participant's host has one generated queue trigger that can therefore
receive directly
addressed messages and subscribed event copies of multiple types. Two
participants
may subscribe to the same topic independently because they have distinct
subscriptions and identity queues.

### Participant declarations

A participant is an attributed class in a shared contracts/topology assembly.
It declares how it participates in the network — nothing more:

- `Processes`: the message contracts this participant receives and handles.
  Processing a message makes the participant its owner; the destination queue
  is the participant's identity queue.
- `Publishes`: the event contracts this participant owns and publishes.
  Publishing an event makes the participant its owner; the topic is derived
  from the participant identity.
- `Subscribes`: the network events this participant wants copies of. Each
  subscribed event must be published by a member of the same network
  (strictly validated), and the participant's supported serializations must
  include the publisher's write protocol.
- `Serializers`: the set of serialization protocols the participant supports
  for the contracts it processes, publishes, or subscribes to.
- `DefaultSerializer`: the participant's write protocol. The wire protocol of
  a message is the *processing* participant's default; the wire protocol of
  an event is the *publisher's* default. Senders look the protocol up in the
  generated registry and never choose it themselves. A default outside the
  declared supported set is a generator error.
- `Retry`: an optional `IMessagingRetryPolicy` type. Retry is participant
  owned: delivery counts, second-level behavior, retry delay, and handler
  duration are per-queue/per-participant concerns, so members may diverge
  freely. A documented framework default applies when omitted.
- `Compression` and `CompressionMinimumSizeBytes`: optional sender-side
  choices. Receive is header-driven and gzip/Brotli are always decodable by
  the runtime, so members may diverge freely.
- `Identity`: optional; defaults to the class name minus a trailing
  `Participant` suffix, normalized to the portable queue-name convention
  (`PrintingFunctionsParticipant` → `printing-functions`). Every participant
  has an identity, including sender-only ones: it feeds
  `amf1-sender-identity`. Identities are 3–50 lowercase ASCII letters,
  digits, or hyphens; they are unique per network and the reserved identity
  `outbox-processor` (including a class name that normalizes to it) is
  rejected.

A participant declaration never names handlers, host-local pipeline steps, an
Azure technology, or a network. Handler registration and step implementation
remain host composition concerns; the network membership comes from the
network's `Members` list (below).

### Shared network/bus configuration

Participant identity is not the configuration boundary for transport behavior. A
Mediator Framework messaging network is the shared operational boundary for
native participants and the shared declaration boundary used to assist Rebus
hosts.
The network is an attributed class whose `Members` list names its participant
types; membership is established solely by that list, and a participant listed
in two networks is a generator error. A participant inherits the ability to
send, receive, publish, and subscribe from its network membership. All native
participants communicating on that network use the same:

- required transport capabilities (see the capability model below);
- maximum transport payload threshold;
- maximum decompressed payload size;
- DataBus offload thresholds and attachment integrity limits — the concrete
  DataBus provider/store and provider-specific lifecycle configuration are
  runtime composition decisions; all participants must compose the same
  provider,
  store, and compatible provider options as a documented deployment
  assumption;
- resource-management and subscription-lifecycle policy; and
- connection/configuration key names, without placing secrets in attributes.

Serialization, compression, and retry are deliberately **not** network
settings: serialization and compression reads are header-driven, and retry
policy is per-queue, so participants declare them individually and may
diverge. The analyzer still enforces the cross-participant constraint that
matters: a subscriber's supported serializations must include the publisher's
write protocol.

The network's contract set is derived from its members' declarations; it is
not listed separately. The same contract declared by members of two different
networks is a generator error, so `amf1-network` remains an exact cross-network
guard.

Rebus generation consumes only the portable or exactly mapped subset described
below. Native serializer/compression/DataBus/pipeline settings do not silently
become Rebus runtime settings.

The network configuration is a public transport-neutral attributed class. The
class name of the
type carrying the attribute is the network identity; the attribute has no
independent `Name` property. It is resolved into one immutable runtime options
object and validated once at startup. A different network
type is a
different messaging network, even when it uses the same Azure namespace.

### Capability model and runtime transport selection

The network does not name a technology. It declares, at definition time, the
transport capabilities it requires. The concrete transport is a runtime
composition decision made for each participant by its host (for example
InMemory when testing, Azure Service Bus in production).

Capabilities are a flags-style set. `Send` is implicit and always required;
the declarable capabilities are:

| Capability | Meaning |
| --- | --- |
| `Receive` | A participant identity can own a queue and receive from it with PeekLock-style settlement |
| `PubSub` | Events can be published to topics and forwarded into subscriber identity queues |
| `ScheduledSend` | `Send` supports delayed delivery by duration or due time |

Each transport implementation declares the capabilities it supports (`Send` is
implicit and universal, so it is not a capability and does not appear here):

| Transport | Receive | PubSub | ScheduledSend |
| --- | --- | --- | --- |
| Azure Service Bus | yes | yes | yes |
| Azure Storage Queue | yes (visibility timeout, `DequeueCount`, poison-queue DLQ) | no | yes (visibility delay) |
| InMemory | yes | yes | yes |

Storage Queue receive is at-least-once through the visibility timeout. It has
no native dead-letter queue, so the transport maps the fixed settlement
contract's dead-letter operation to a framework-managed `<queue>-poison`
companion queue, and maps the native `DequeueCount` to the delivery count.
Storage Queue has no topics, so it never supports `PubSub`.

Validation is split by binding time:

- **Compile time** derives each member's capability needs from its
  declarations — `Processes`/`Subscribes` require `Receive`,
  `Publishes`/`Subscribes` require `PubSub`, delayed-send usage requires
  `ScheduledSend` (where statically visible) — and validates the needs against
  the network's declared `Requires`. A member whose needs exceed the declared
  capabilities is a diagnostic naming the capability and the member.
  Compile time never checks a transport, because the transport is unknown.
- **Startup** validates the composed transport against the network
  declaration: registering a transport that does not support every declared
  capability fails startup with an explicit diagnostic.
- **Runtime** guards remain for dynamic operations: delayed `Send` and
  `Publish` throw when the capability is absent from the network declaration.

A network that only requires `Send` can therefore run on every transport; a
network requiring `PubSub` can run only on transports that support it.

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
| Network member `Processes` declarations | Existing type-based owner routing for every processed message, targeting the processing participant's identity queue | Transport implementation, connection, credentials |
| Consumer participant identity and `Processes`/`Subscribes` | Participant-filtered Rebus dispatch adapters for exactly the declared contracts; generated descriptor exposes the identity queue name | Application-handler registration, input transport selection, queue creation policy, workers/concurrency |
| Participant event subscriptions (`Subscribes`) | Participant-filtered event dispatch adapters plus an async generated method that calls `Subscribe<TEvent>` after the bus starts | Application-handler registration, subscription storage, broker administration |
| Participant with no `Processes`/`Subscribes` (sender-only or publisher) | Routing and bus adapter only; no input queue, handlers, or subscriptions | One-way transport and lifecycle |
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
| **Network** | The shared messaging boundary: an attributed class listing its member participants, the required transport capabilities, and the shared payload/DataBus thresholds, resource lifecycle, and connection key names. Its contract set is derived from member declarations. Also supplies portable setup metadata to Rebus generation. |
| **Participant** | One logical member of a network, declared as an attributed class: which messages it processes, which events it publishes, which events it subscribes to, and which serializations it supports. A participant may produce only, consume through generated Azure Functions triggers, or consume through an assisted Rebus composition. |
| **Host** | The deployable process and hosting technology that runs a participant: an Azure Functions app with generated triggers, a Rebus-based worker, or a test/custom host running the InMemory pump or the outbox processor. The host binds to the participant declaration and selects the concrete technology; the participant declaration never does. |
| **Identity** | The portable logical name of a participant, defaulting to its normalized class name. For a consumer it is also the name of its single receive queue. Every participant has one, including sender-only participants (it feeds `amf1-sender-identity`). |
| **Ownership** | Conferred solely by participant declaration: the participant listing a message in `Processes` owns it; the participant listing an event in `Publishes` owns it. Contracts are owner-free. |
| **Queue** | A point-to-point inbox named by a participant identity. A message's destination is the identity queue of the participant processing it. Event publisher ownership never implies queue delivery. |
| **Subscription** | An explicit participant selection (`Subscribes`) of a network event. Service Bus forwards that event into the subscriber participant's identity queue. |
| **Sender identity** | The stable identity written to `amf1-sender-identity` for the participant that invoked `Send` or `Publish`. It is routing-neutral and remains the original sender when an outbox processor later dispatches the envelope. |

Conceptual shape:

```csharp
[MessagingNetwork(
    Members = new[]
    {
        typeof(PrintingParticipant),
        typeof(WebFrontendParticipant)
    },
    Requires = MessagingCapabilities.Receive
        | MessagingCapabilities.PubSub
        | MessagingCapabilities.ScheduledSend,
    MaximumTransportPayloadBytes = 240000)]
public sealed class BookMessagingNetwork;

/// <summary>Retry/delivery policy owned by the declaring participant.</summary>
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

// Consumer participant (hosted in Azure Functions): processes PrintBook and
// subscribes to BookPrintCompleted. Identity defaults to "printing"; its
// identity queue is its receive queue.
[MessagingParticipant(
    Processes = new[] { typeof(PrintBook) },
    Subscribes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[]
    {
        SerializationProtocol.Json,
        SerializationProtocol.MessagePack
    },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(BookRetryPolicy),
    Compression = CompressionAlgorithm.Brotli,
    CompressionMinimumSizeBytes = 4096)]
public sealed partial class PrintingParticipant;

// Publisher participant (run by any process: Minimal API, client app,
// Functions): owns and publishes BookPrintCompleted; no queue, no trigger,
// no subscriptions, only a configured IBus. Identity: "web-frontend".
[MessagingParticipant(
    Publishes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class WebFrontendParticipant;

// Functions host binding (in the Functions host assembly): attaches this
// host to the participant, selects the trigger binding, and adds host-local
// pipeline steps. The participant declaration stays transport-neutral.
[assembly: MessagingFunctionsHost(
    typeof(PrintingParticipant),
    MessagingFunctionsTriggerBinding.ServiceBus,
    IncomingSteps = new[] { typeof(BookUserContextIncomingStep) },
    OutgoingSteps = new[] { typeof(BookUserContextOutgoingStep) })]
```

The exact `IMessagingRetryPolicy` member set is finalized by the implementation
tasks. DataBus provider options, including minimum attachment lifetime, belong
to runtime provider composition rather than the network declaration. Both
contain no secrets and are validated once at startup.

The final API may use a configuration object instead of the conceptual
attribute, but the member-list and participant-declaration invariants are
fixed.
Compile-time diagnostics reject participants missing from every network,
participants listed in two networks, contracts processed or published by zero
or multiple members, unsatisfiable subscriptions, usage exceeding the declared
capabilities, serializer incompatibilities between publishers and
subscribers, and network-level declarations of participant-owned settings.
Runtime startup
rejects divergent effective options and capability-insufficient transports.

### Participant roles

Roles are inferred from the declarations, never declared. A participant can be
a consumer and a publisher at once:

- **Consumer** (`Processes` or `Subscribes` non-empty): owns the identity
  queue named by its identity, receives the contracts it declares, and gets at
  most one generated trigger when hosted in Azure Functions. Handler
  registration for every declared contract is validated at startup.
- **Publisher** (`Publishes` non-empty): owns the topics of its events; the
  resource lifecycle creates them. A publisher that consumes nothing owns no
  queue and gets no trigger.
- **Sender-only** (all three lists empty): owns no contracts, queue, trigger,
  or subscriptions, but still has an identity — used for
  `amf1-sender-identity` — and may send any network message to its processing
  participant's identity queue.

Azure Functions is the only supported native-network hosting technology for
consumer
participants and the only host with generated triggers. Rebus consumers use the
same
participant/contract metadata through the separate generated Rebus assistance
above.
Producing is universal: any process that composes the transport-neutral
messaging runtime — a Minimal API project, a console/client application, or a
Functions app — participates as a sender through the configured `IBus`
alone.

Capability needs follow the declarations: consumers require `Receive`;
publishers and subscribers require `PubSub`. A network declaring only implicit
`Send` (optionally plus
`ScheduledSend`) permits sender-only participants and publisher-less members
on any transport.

### Proposed API shape

The implementation task will select final public names, XML documentation, and
API-surface entries, but the model is fixed:

```csharp
[Message]
public sealed record RecalculateOrder : ICommand<RecalculateOrder>;

[Event]
public sealed record OrderRecalculated : ICommand<OrderRecalculated>;

// identity: "billing" (normalized class name); processes RecalculateOrder,
// subscribes to OrderRecalculated.
[MessagingParticipant(
    Processes = new[] { typeof(RecalculateOrder) },
    Subscribes = new[] { typeof(OrderRecalculated) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class BillingParticipant;

[MessagingNetwork(Members = new[] { typeof(BillingParticipant) })]
public sealed class BillingMessagingNetwork;
```

The message/event attributes must not reference Azure SDK types or Rebus types.
The participant attribute declares identity (optional, defaulting to the
normalized class name), processed messages, published events, subscriptions,
supported serializations and write default, and participant-owned retry and
compression settings. It never lists handlers, host-local steps, a network
reference, or an Azure
technology: the Functions host binding is a separate assembly-level attribute
in the Functions host
project (see §6). A participant belongs to a network solely by appearing in
its `Members` list; membership in two networks is a generator error.

Participant identities — explicit or normalized from the class name — use one
portable queue-name
contract so runtime transport selection never silently changes an address:
3–50 lowercase ASCII letters, digits, or hyphens; the first and last character
must be alphanumeric; consecutive hyphens are invalid. The identity doubles as
the consumer's queue name and the publisher's topic prefix, so no separate
owner names exist. Reserved names are rejected at compile
time: the identity `outbox-processor` is reserved for the framework outbox
processor — whether written explicitly or produced by normalizing the class
name — and identities ending in `-poison` are reserved for
framework-managed companion queues. The generator diagnoses violations and
routes each message to the identity queue of the participant that processes
it. It diagnoses unsatisfiable
subscriptions, contracts declared by zero or multiple members, and duplicate
member registrations. Event topic derivation uses the publisher identity and
the normalized logical
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
properties, so its body is the canonical, single-Base64 envelope described in
§5, containing the binary payload and the same header set. The Storage Queue
encoder must not assume that the payload is JSON merely because its outer
representation is text. The InMemory transport stores the envelope as-is.
Envelope construction and interpretation are transport-neutral; each transport
adapter owns only the mapping between the envelope and its native message
shape.

Read protocol selection is always driven by the header. Consumers do not use
any participant default, compression threshold, or retry settings to interpret
an
incoming message. They use `amf1-content-type`, `amf1-content-encoding`, and
the contract type header as authoritative and accept every installed supported
protocol/encoding implementation, regardless of the sender's or owner's write
default.
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
They also write `amf1-sender-identity` for both `Send` and `Publish`. Every
participant has an identity — explicit, or the normalized class name — so the
sender identity is always the sending participant's identity; there are no
identity-less senders. The sender header
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

Write protocol selection is owned by the contract's owner: the wire protocol
of a message is the processing participant's `DefaultSerializer`, and the wire
protocol of an event is the publishing participant's `DefaultSerializer`.
Senders resolve the protocol through the generated registry and never choose
it. A default outside the declaring participant's supported set is a
compile-time diagnostic, as is a subscriber whose supported set excludes the
publisher's write protocol.
The runtime serializer registry is pluggable and ships integrations for the
repository's supported JSON, MessagePack, and protobuf abstractions. Startup
composition validates that the installed codecs cover the participant's
declared supported set; sending with an uninstalled owner protocol fails fast
with a targeted error.

Compression is selected per participant (sender side). Payloads below the
participant's configured
minimum compression size are sent uncompressed. Larger payloads use the
participant's configured gzip or Brotli encoding and set
`amf1-content-encoding`; the header
is absent when the payload is uncompressed. Receive is header-driven and both
encodings are always decodable by the runtime, so members may diverge freely.
The compressed serialized bytes are
then compared with the network-configured maximum payload threshold. The
runtime also constructs the complete candidate inline envelope and asks the
composed transport to measure its final native representation, including
headers and transport encoding. If either threshold is exceeded, those exact
compressed bytes are stored in the shared DataBus and the envelope carries the
attachment ID instead of the body. The attachment-reference envelope is
measured again and fails explicitly if it cannot fit. Consumers fetch the
attachment from the same shared DataBus, then decompress and deserialize it.

All participants on one messaging network share payload and
DataBus thresholds through the network, and must compose the same DataBus
provider for attachment access. A consumer remains
header-driven and must not silently replace an incoming protocol or encoding
with a local default.

## 5. Transport abstraction, packaging, and InMemory transport

The runtime depends on one internal-facing transport contract, not on Azure
SDK types. A transport implementation provides:

- a declared `MessagingCapabilities` set;
- a hard maximum inline-envelope ceiling in bytes plus a deterministic
  measurement seam. The measurement evaluates the completed native
  representation of an envelope, including headers and transport encoding;
  claim-check decisions must not use payload bytes alone (§4, §11);
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
   artifacts. InMemory has no hard inline-envelope ceiling; the network
   payload threshold applies alone.
2. **Azure Service Bus** — full capability set. Its receive side is bound by
   generated Azure Functions triggers. Hard ceiling: 256 KB total standard-tier
   message size including application properties. The transport measures the
   complete native message; the recommended 240 000-byte network payload
   threshold leaves headroom but does not replace that measurement.
3. **Azure Storage Queue** — send, scheduled send (visibility delay), and
   at-least-once receive through a generated isolated `QueueTrigger`. Hard
   ceiling: 64 KiB for the final queue-message text. The canonical binary
   envelope includes its header map and binary payload, then is Base64-encoded
   exactly once. A normal inline envelope is capped at 46 080 canonical bytes
   and reserves 3 072 bytes for bounded poison metadata; a poison envelope is
   consequently capped at 49 152 canonical bytes, which Base64-encodes to at
   most 64 KiB. The transport measures the final encoded text before send and
   before claim-check, including the complete headers and the poison-metadata
   reservation. The Azure SDK client uses `QueueMessageEncoding.None`, and
   generated Functions hosts require `extensions.queues.messageEncoding` =
   `none`, so sender and receiver each perform exactly one Base64 operation.
   Isolated
   QueueTrigger has no `MessageActions` equivalent: the Functions host deletes
   the message when the function returns successfully, and applies
   `host.json` `queues.visibilityTimeout` (default zero) when the function
   throws. After `queues.maxDequeueCount` failed invocations the host moves
   the message to `<queue>-poison` with no application metadata. This is
   **not** Service Bus PeekLock. The generated function binds
   `QueueMessage` (extension 5.2.0+) so `DequeueCount`, `MessageId`, and
   `PopReceipt` are available. No `PubSub`.

The network payload threshold remains an application-size guard. Every send
also measures its complete inline envelope against the composed transport's
native ceiling, so a network sized for Service Bus offloads safely when
composed over Storage Queue. A network intended for Storage Queue should set
its threshold at or below 46 080 bytes to make most offload behavior explicit,
while still relying on final-envelope measurement for header variation.
Startup warns when the configured threshold exceeds the composed transport's
practical inline ceiling.

Storage Queue settlement under QueueTrigger is therefore Functions-native
except for immediate poison:

| Dispatcher decision | Generated function |
| --- | --- |
| Complete | Return successfully. The host deletes the message. |
| Abandon (retry) | Throw. Do not catch. The host applies `visibilityTimeout` = the participant's `RetryDelay`. |
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
`host.json` is not generated. A Functions app hosts exactly one bound messaging
participant, so its host-wide queue retry settings map unambiguously to that
participant's policy. A Storage Queue messaging Functions app must not contain
an unrelated QueueTrigger that requires conflicting `queues` settings.
Required values:

- `queues.messageEncoding` = `none`, because the framework owns the canonical
  single Base64 envelope encoding;
- `queues.visibilityTimeout` = the participant's `RetryDelay` (must not stay
  at the default zero);
- `queues.maxDequeueCount` = `2 × MaximumDeliveryCount` when the participant
  enables second-level retries, otherwise `MaximumDeliveryCount`.

When `host.json` is supplied through `AdditionalFiles`, the generator warns
when `messageEncoding` is not literal `none`, or these retry keys are missing
or malformed. It cannot execute a runtime retry-policy type, so exact
expected-versus-actual comparison belongs to startup after the immutable
network options are resolved. When the generator cannot inspect `host.json`,
it emits an information diagnostic. Startup logs expected versus actual values,
with an opt-in strict mode that fails startup.

A single transport-contract conformance test suite runs against every
transport (for Azure transports, against Azurite or the Azure Service Bus
emulator, both runnable in Docker) so InMemory
semantics cannot drift from production transports.

### Packaging

The transport-neutral messaging runtime — network options, message context,
codecs, pipeline, DataBus, transports, dispatcher, and the restricted `IBus` —
lives in the `Ark.MediatorFramework.Messaging` namespace of the
`Ark.Tools.MediatorFramework` assembly (a `Messaging/` sub-folder) so
send-only participants such as a Minimal API host or a client application
compose the bus without referencing anything Functions-flavored. The Azure
Functions package contains only trigger source generation and Functions
hosting adapters and depends on the core MediatorFramework assembly.
Messaging source generation (network validation and the participant-owned
contract mappers) lives in the generic
`Ark.Tools.MediatorFramework.Messaging.Generators` project, which is tied to
neither Azure Functions nor the ApiSurface generator and ships as an analyzer
inside the `Ark.Tools.MediatorFramework` package. Task documents that name
`Ark.Tools.MediatorFramework.AzureFunctions` for runtime seams are satisfied
by the messaging runtime in the core assembly; the split is finalized in the
package/composition task.

## 6. Generated Functions surface

The incremental generator consumes:

1. the network declaration and its member participants' declarations
   (`Processes`, `Publishes`, `Subscribes`, serializations, identity);
2. the Functions host binding referencing the participant type;
3. the shared network configuration.

It emits:

- at most one stable trigger for the single participant bound to the Functions
  app, when that
  participant declares `Processes` or `Subscribes` — a Service Bus trigger
  or a
  Storage Queue trigger, selected by the Functions host binding's compile-time
  trigger selection;
- typed calls into a runtime dispatch helper;
- a deterministic subscription/resource manifest for startup;
- compile-time diagnostics for unwired or multiply-owned contracts,
  unsatisfiable subscriptions, invalid names, reserved-name usage,
  duplicate members, usage exceeding the declared network capabilities,
  protocol
  incompatibilities, unsupported contract shapes, and trigger-name collisions.

Azure Functions trigger attributes are compile-time facts, but the
transport-neutral participant declaration never names an Azure technology. A
Functions host running a receive-capable
participant therefore binds itself to the participant and selects the trigger
binding (Service
Bus or Storage Queue) through one assembly-level attribute in the
Azure Functions package,
`[assembly: MessagingFunctionsHost(typeof(PrintingParticipant), MessagingFunctionsTriggerBinding.ServiceBus)]`,
consumed by the generator — nothing Azure-specific appears on
`[MessagingParticipant]`. A Functions app may bind exactly one messaging
participant; multiple `MessagingFunctionsHost` bindings are a compile-time
diagnostic and startup rejects a descriptor that differs from the single
generated binding. This
selection is the single source of truth
for the host's receive transport: the generated manifest records it, and
startup composition fails when the composed runtime transport does not match
the recorded binding, because the generator-side attribute and the runtime
composition are different files that can drift. Participants that consume
nothing (sender-only and
publisher-only participants) and
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

When the bound participant declares no `Processes` and no `Subscribes`, no
receive
trigger or subscription manifest entry is emitted and the generator reports an
information diagnostic that the Functions host is send-only.
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
Custom pipeline steps are registered per participant on its host binding, not
on the shared network
configuration or the participant declaration (§10); the network owns only the
stable stage contract and stage
identifiers.

One queue trigger represents one named participant identity.
`MessagingParticipant` declares
contracts only; it never selects handlers. The generated descriptor is
validated against SimpleInjector registrations at startup: every contract in
`Processes` and `Subscribes` must have exactly one handler in the participant's
host composition. The same event may be subscribed by multiple participants
because
each subscription forwards a separate Service Bus copy into its participant's
identity queue.

Let `N` be the participant's `MaximumDeliveryCount` (from its declared or
default retry policy). When second-level retries are
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
delivery `N`. The participant's retry policy enables the second-level stage;
handler presence is
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

- validates that every sent message is processed by exactly one member of the
  network and routes it to that participant's identity queue;
- routes events to `<publisher-identity>-<contract-name>`;
- permits `Publish` only when the network declares `PubSub` and the current
  participant declares the event in its `Publishes` set;
- permits delayed `Send` only when the network declares `ScheduledSend`;
- selects and applies the write protocol of the contract's owner (the
  processing participant's default for messages, the publisher's default for
  events);
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
processing. Steps are registered per participant on its host binding, not on
the network or the
participant declaration: implementations
can carry heavy host-only dependencies and participants/environments
may intentionally
select different step sets or ordering. The network defines the stable stage
contract only. Each participant resolves its own steps through its host's
composition
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
bytes with the network-configured maximum payload threshold and measures the
complete candidate native envelope (§5). If either guard is exceeded, the
compressed bytes are stored in DataBus and the transport message carries
`amf1-payload-attachment-id`. The attachment-reference envelope is measured
again and fails explicitly if it still cannot fit. Consumers transparently
fetch the attachment, verify byte length and SHA-256 metadata, decompress
within the configured output bound if required, and deserialize.

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
one contract assembly and one network declaration:

1. **Publisher participant:** declares `Publishes` for the event, consumes
   nothing, and is run by a non-Functions host (for example the Minimal API
   web host); it owns
   the event topic, owns no queue, and has no event handler.
2. **Subscriber A:** declares the event in `Subscribes` and registers handler
   A.
3. **Subscriber B:** declares the same event in `Subscribes` independently and
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
registry and participant ownership metadata, and application handlers. Rebus
registers adapters to its
native bus and failed-message API. The Rebus publisher-only and consumer
participants also reference the same network/participant
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
declaration whose identity — explicit or normalized from the class name —
equals it, and startup rejects any composition attempting to register a
participant under it. It owns no receive queue or event
subscriptions. It
peek-locks durable outbox batches, sends their already validated raw envelopes
through the configured network transport, and commits deletion only after the
transport send succeeds. The reserved identity identifies the running
processor operationally; it does not overwrite envelope sender identity or
bypass the `Publishes` ownership check at enqueue time.

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
  routing, protocol conflicts, capability-usage violations, and rejection of
  multiple messaging participant bindings in one Functions app.
- Envelope/serializer tests cover all three protocols, multiple types in one
  queue, missing installed codecs, malformed headers, binary payloads,
  compression, and DataBus claim-check.
- Pipeline tests cover ordering, custom header propagation, user context,
  OpenTelemetry context, and exception/cancellation propagation. AZM-09
  verifies the resulting settlement behavior.
- Rebus generator tests cover sender-only/publisher versus consumer
  participant output,
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
  `visibilityTimeout` = `RetryDelay`, the canonical single-Base64
  `messageEncoding: none` envelope, normal/poison final-size boundaries, the
  SDK immediate-poison path (including host delete-miss after
  `DeleteMessage`), generator `host.json` diagnostics, and the startup
  expected-versus-actual warning.
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
| Network payload threshold or headers exceed the composed transport's hard ceiling | Measure the completed native envelope before send and claim-check; reserve Storage Queue poison-metadata capacity; startup warns when the payload threshold exceeds the practical inline ceiling |
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
| Functions runtime poisons Storage Queue messages before framework exhaustion | One participant per Functions app makes the host-wide retry policy unambiguous; visible `host.json` `maxDequeueCount` contract, generator diagnostic, startup warning with strict opt-in |
| Blob attachment expires while a message remains deliverable | Provider-specific minimum lifetime plus IaC lifecycle policy; operators include TTL, backlog, outages, scheduling, and retry windows |
| Host connected to a different network type | `amf1-network` fails fast. Same-type wrong namespace is undetectable; DataBus mismatches fail on length/hash |
| InMemory receive pump started inside a Functions app | Functions composition rejects the InMemory receive transport; InMemory participants use test/custom hosts; Functions e2e testing uses Azurite or the Service Bus emulator |
| Message/event API confusion | Separate attributes, diagnostics, and routing rules |
| Rebus semantic drift | Reuse header names and failure concepts without coupling the new runtime to Rebus |
