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
  consumer host (Service Bus PeekLock or Storage Queue QueueTrigger);
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
- demonstrates one publisher and two independent subscriber Function hosts
  sharing contracts while using different handlers.

Azure Functions is the only supported Processor/Consumer host and the only
host with trigger source generation. Producer-only participants are first
class: any process — a Minimal API host, a console client, another service —
can join a network as a one-way producer by composing only the configured
`IBus` from the transport-neutral messaging runtime. Storage Queue supports
sending, scheduled sending, and at-least-once receive through generated
QueueTriggers; it never supports event publishing (no topics).

## 2. Explicit boundaries

### In scope

- A new transport-neutral message attribute with one destination owner queue.
- A new transport-neutral event attribute with one canonical publisher owner.
- A shared network/bus configuration referenced by every participating host.
- Assembly-level host identity, role, subscriptions, and network reference.
- Generated one-trigger-per-named-host identity queue for Service Bus and
  Storage Queue (each behind the Functions host project's compile-time
  trigger selection).
- Service Bus subscriptions that forward event copies into the subscriber
  host's identity queue.
- Runtime binding, envelope decoding, typed dispatch, scoped dependencies,
  settlement, retries, dead-lettering, and second-level dispatch.
- Service Bus `Send`, `Publish`, and delayed send.
- Storage Queue `Send`, visibility-delay scheduling, and at-least-once receive
  with `DequeueCount` and a framework-managed poison-queue DLQ.
- Producer-only hosts in any process (Functions, Minimal API, client apps)
  composing only the configured `IBus`.
- A transport-neutral messaging runtime package consumable outside Azure
  Functions; the Functions package adds trigger generation and hosting
  adapters.
- Shared DataBus claim-check storage for every transport.
- Extensible incoming/outgoing pipeline steps for context propagation and
  transport concerns.
- Concurrency-safe declaration and removal of host-owned Service Bus
  subscriptions.
- A capability-based transport abstraction. Networks declare required
  capabilities at definition time; the concrete transport is selected at
  runtime composition.
- A first-class, shipped InMemory transport implementing every capability. It
  is a real transport usable for tests and local development, not a mock. The
  same transport-contract conformance suite runs against every transport.
- A three-host sample demonstration.

### Out of scope

- Durable outbox support for Mediator Framework networks. A non-durable
  passthrough commit dispatcher is allowed only to keep the sample application
  composition transport-neutral.
- Request/reply, replies, `SendLocal`, or any receive operation in the bus shim.
- Storage Queue subscriptions or publish fan-out emulation. Storage Queue has
  no topics; `PubSub` networks cannot run on it.
- Rebus wire interoperability. Rebus header concepts are retained, but the
  new envelope and generated host are not required to exchange messages with
  existing Rebus endpoints. Interoperability is neither required nor expected,
  so no test asserts its absence; the boundary is documentation-only.
- A long-running worker hosted inside a Functions app.
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
messages. Receiving a message requires exactly one handler registration for
the queue/contract combination in a host. A named host receives only from the
queue whose name equals its `Identity`; every contract listed in that host's
`ReceivedContracts` must therefore declare an owner queue equal to the host
identity. Handler registration is validated separately during startup.
Scale-out instances of the same host identity compete normally on that one
queue.

Every message/event has a stable logical contract identity. By default it is
the namespace-qualified CLR type name without assembly version. `[Message]`
and `[Event]` may override it with an explicit stable `Name` and may declare
`FormerNames` aliases for compatible CLR renames. The generator rejects
duplicate current names, duplicate aliases, alias cycles, and an alias that is
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

The contract-name segment is the current logical contract identity normalized
to Azure Service Bus naming constraints. Because changing the current logical
name changes the topic, event renames require an explicit topology migration;
`FormerNames` supports reading old queued messages but does not implicitly
merge or rename topics.

The event is published once to the topic and is cloned by Service Bus into
subscriber queues. A host declares its identity and the event contracts it
subscribes to. Each subscription forwards its copy into the subscriber host's
identity queue. The host has one generated queue trigger that can therefore
receive directly addressed messages and subscribed event copies of multiple
types. Two hosts may subscribe to the same topic independently because they
have distinct subscriptions and identity queues.

The generator must reject an event declared with a missing publisher owner and
must reject event usage on a network that does not declare the `PubSub`
capability.

### Shared network/bus configuration

Host identity is not the configuration boundary for transport behavior. A
Mediator Framework messaging network is the shared operational boundary for
all participating non-Rebus hosts. Every host references exactly one network
configuration, and all hosts communicating on that network use the same:

- required transport capabilities (see the capability model below);
- active serialization protocols and default protocol;
- compression algorithm and minimum compression size;
- maximum transport payload threshold;
- maximum decompressed payload size;
- DataBus offload thresholds and attachment integrity limits — the concrete
  DataBus provider/store and provider-specific lifecycle configuration are
  runtime composition decisions; all hosts must compose the same provider,
  store, and compatible provider options as a documented deployment
  assumption;
- retry and delivery-count policy;
- resource-management and subscription-lifecycle policy; and
- connection/configuration key names, without placing secrets in attributes.

The network configuration is a public transport-neutral type or declarative
attribute that can be referenced by a host attribute. The class name of the
type carrying the attribute is the network identity; the attribute has no
independent `Name` property. It is resolved into one immutable runtime options
object and validated once at startup. Host attributes contain an optional
identity, explicitly received message contracts, and event subscriptions; they
never select or register handlers and must not redefine network settings.
Handler registration remains a SimpleInjector composition concern. A host
referencing a different network profile is a different messaging network,
even when it uses the same Azure namespace.

### Capability model and runtime transport selection

The network does not name a technology. It declares, at definition time, the
transport capabilities it requires. The concrete transport is a runtime
composition decision made by each host (for example InMemory when testing,
Azure Service Bus in production).

Capabilities are a flags-style set. `Send` is implicit and always required;
the declarable capabilities are:

| Capability | Meaning |
| --- | --- |
| `Receive` | A host identity can own a queue and receive from it with PeekLock-style settlement |
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

- **Compile time** validates usage against the network declaration. A host
  with an `Identity` on a network without `Receive`, a subscription or
  `[Event]` usage on a network without `PubSub`, and delayed-send usage on a
  network without `ScheduledSend` (where statically visible) are diagnostics.
  Compile time never checks a transport, because the transport is unknown.
- **Startup** validates the composed transport against the network
  declaration: registering a transport that does not support every declared
  capability fails startup with an explicit diagnostic.
- **Runtime** guards remain for dynamic operations: delayed `Send` and
  `Publish` throw when the capability is absent from the network declaration.

A network that only requires `Send` can therefore run on every transport; a
network requiring `PubSub` can run only on transports that support it.

That all hosts participating in one network use the same transport and the
same physical resources (broker namespace, DataBus store) is a runtime
operational fact and a documented deployment assumption. Each host validates
only its own composed transport against the shared network declaration; no
cross-host runtime check is performed.

Rebus hosts do not participate in a Mediator Framework messaging network.
Rebus and Mediator Framework transports have different headers and runtime
semantics and are not wire-interoperable. Every deployment topology must choose
one receiver stack for a logical bus: either Rebus hosts or Mediator Framework
network hosts. They may reuse the same transport-neutral contract ownership
metadata and application handlers, but not exchange persisted messages. This
boundary is informative documentation; because interoperability is neither
required nor expected, no test asserts its absence.

Conceptual shape:

```csharp
[MessagingNetwork(
    Requires = MessagingCapabilities.Receive
        | MessagingCapabilities.PubSub
        | MessagingCapabilities.ScheduledSend,
    DefaultSerializer = SerializationProtocol.Json,
    Compression = CompressionAlgorithm.Brotli,
    CompressionMinimumSizeBytes = 4096,
    MaximumTransportPayloadBytes = 240000,
    Retry = typeof(BookRetryPolicy),
    IncomingSteps = new[] { typeof(BookUserContextIncomingStep) },
    OutgoingSteps = new[] { typeof(BookUserContextOutgoingStep) })]
public sealed class BookMessagingNetwork;

/// <summary>Retry/delivery policy shared by every host on the network.</summary>
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

// Consumer host (Azure Functions): owns the identity queue and a trigger.
[assembly: MessagingHost(
    Identity = "printing-functions",
    Network = typeof(BookMessagingNetwork),
    ReceivedContracts = new[] { typeof(PrintBook) },
    Subscriptions = new[] { typeof(BookPrintCompleted) })]

// Producer-only host (any process: Minimal API, client app, Functions):
// the identity grants event-publish ownership only; no queue, no trigger,
// no subscriptions, only a configured IBus.
[assembly: MessagingHost(
    Identity = "web-frontend",
    Role = MessagingHostRole.Producer,
    Network = typeof(BookMessagingNetwork))]
```

The exact `IMessagingRetryPolicy` member set is finalized by the implementation
tasks. DataBus provider options, including minimum attachment lifetime, belong
to runtime provider composition rather than the network declaration. Both
contain no secrets and are validated once at startup.

The final API may use a configuration object instead of the conceptual
attribute, but the host reference and shared-network invariants are fixed.
Compile-time diagnostics reject missing network references, duplicate
declarations for the same network type, usage exceeding the declared
capabilities, and host-local overrides of shared settings. Runtime startup
rejects divergent effective options and capability-insufficient transports.

### Host roles

Azure Functions is the only supported Processor/Consumer host and the only
host with generated triggers. Producing is universal: any process that
composes the transport-neutral messaging runtime — a Minimal API project, a
console/client application, or a Functions app — participates as a producer
through the configured `IBus` alone.

`Identity` on `MessagingHost` is optional and its meaning depends on the host
role:

- **Consumer (default role, named identity)**: the host processes the queue
  named by its identity, declares the message contracts it receives, may
  declare event subscriptions, and may publish events whose canonical owner
  is that identity. Every received message contract must declare that same
  identity as its owner queue. It gets at most one generated trigger.
- **Producer (explicit `Role = Producer`, named identity)**: the identity
  grants event-publish ownership only. The host owns no queue, gets no
  trigger, declares no received contracts or subscriptions, and selects no
  handlers; declaring received contracts or subscriptions is a compile-time
  diagnostic. The resource lifecycle creates only the topics for events owned
  by that identity. Typical hosts: the Minimal API web frontend or a client
  application that sends commands and publishes its own events.
- **Sender-only (no identity)**: no queue, no trigger, no subscription, and no
  publish; the host may still send messages to their declared owner queues.

A named consumer identity requires the network to declare `Receive`; a
subscription requires `PubSub`; a producer identity requires `PubSub` only
when it owns events. A network declaring only implicit `Send` (optionally plus
`ScheduledSend`) permits identity-less senders and producers without owned
events on any transport.

### Proposed API shape

The implementation task will select final public names, XML documentation, and
API-surface entries, but the model is fixed:

```csharp
[Message(OwnerQueue = "orders")]
public sealed record RecalculateOrder : ICommand<RecalculateOrder>;

[Event(OwnerPublisher = "orders")]
public sealed record OrderRecalculated : ICommand<OrderRecalculated>;

[assembly: MessagingHost(
    Identity = "billing",
    Network = typeof(BillingMessagingNetwork),
    ReceivedContracts = new[] { typeof(RecalculateOrder) },
    Subscriptions = new[] { typeof(OrderRecalculated) })]
```

The message/event attributes must not reference Azure SDK types or Rebus types.
The assembly-level host attribute is host-specific and contains an optional
identity, an optional role, received message contracts, subscriptions, and a
reference to the shared network configuration. It must not contain independent serialization,
compression, DataBus, transport, or retry values — and it never names an
Azure technology: the Functions trigger binding is selected in the Functions
host project setup instead (see §6). A host with no identity is
valid only as a sender-only host and cannot declare received contracts or
subscriptions.

Message owner queues and named host identities use one portable queue-name
contract so runtime transport selection never silently changes an address:
3–63 lowercase ASCII letters, digits, or hyphens; the first and last character
must be alphanumeric; consecutive hyphens are invalid. Event publisher
identities use the same convention. The generator diagnoses violations and
requires `ReceivedContracts` ownership to match `MessagingHost.Identity`
ordinally. Event topic derivation remains Service Bus-specific and separately
normalizes the logical contract-name segment with collision diagnostics.

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
DataBus store: those hosts write the same identity. Wrong-store attachments
still fail on length/hash checks. Wrong-namespace same-type topology is a
documented operational assumption, not a wire check.
Senders always write the resolved network identity.

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
then compared with the configured maximum transport payload size. If they are
still too large, those exact compressed bytes are stored in the shared DataBus
and the envelope carries the attachment ID instead of the body. Consumers
fetch the attachment from the same shared DataBus, then decompress and
deserialize it.

All hosts on one messaging network must share serialization, compression, and
DataBus configuration for sending and attachment access. A consumer remains
header-driven and must not silently replace an incoming protocol or encoding
with the network default.

## 5. Transport abstraction, packaging, and InMemory transport

The runtime depends on one internal-facing transport contract, not on Azure
SDK types. A transport implementation provides:

- a declared `MessagingCapabilities` set;
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
   generated Functions triggers.
2. **Azure Service Bus** — full capability set. Its receive side is bound by
   generated Azure Functions triggers.
3. **Azure Storage Queue** — send, scheduled send (visibility delay), and
   at-least-once receive through a generated isolated `QueueTrigger`. Isolated
   QueueTrigger has no `MessageActions` equivalent: the Functions host deletes
   the message when the function returns successfully, and applies
   `host.json` `queues.visibilityTimeout` (default zero) when the function
   throws. After `queues.maxDequeueCount` failed invocations the host moves
   the message to `<queue>-poison` with no application metadata. This is
   **not** Service Bus PeekLock. The generated function binds
   `QueueMessage` (extension 5.2.0+) so `DequeueCount`, `MessageId`, and
   `PopReceipt` are available. No `PubSub`.

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
transport (for Azure transports, where infrastructure permits) so InMemory
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

1. the host's explicitly received message contracts, subscribed events, and
   their routing metadata;
2. the optional Functions host identity and role;
3. the referenced shared network configuration.

It emits:

- one stable trigger for the named consumer host's identity queue when that
  host receives messages or subscribed events — a Service Bus trigger or a
  Storage Queue trigger, selected by the Functions host project's compile-time
  trigger selection;
- typed calls into a runtime dispatch helper;
- a deterministic subscription/resource manifest for startup;
- compile-time diagnostics for missing owners, invalid names, duplicate
  ownership, usage exceeding the declared network capabilities, protocol
  conflicts, unsupported contract shapes, and trigger-name collisions.

Azure Functions trigger attributes are compile-time facts, but the
transport-neutral host declaration never names an Azure technology. A
receive-capable Functions host therefore selects the trigger binding (Service
Bus or Storage Queue) through a dedicated assembly-level attribute in the
Azure Functions package,
`[assembly: MessagingFunctionsTrigger(MessagingFunctionsTriggerBinding.ServiceBus)]`,
consumed by the generator — rather than on `[MessagingHost]`. This selection
is the single source of truth
for the host's receive transport: the generated manifest records it, and
startup composition fails when the composed runtime transport does not match
the recorded binding, because the generator-side attribute and the runtime
composition are different files that can drift. Producer-only and
InMemory-composed hosts are exempt; for them the transport remains a pure
runtime composition decision. A host composed with the InMemory transport does
not use generated triggers: its receive side runs through the InMemory runtime
message pump against the same dispatcher. The generated trigger is therefore an
Azure hosting adapter over the transport-neutral dispatch runtime, not the
dispatch runtime itself.

When the host identity is absent or the host role is Producer, no receive
trigger or subscription manifest entry is emitted. Generated Service Bus
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
user-context and OpenTelemetry steps are available but opt-in per network.
Custom pipeline steps are registered on the shared network configuration so
all hosts on that network use the same ordering and propagation behavior.

One queue trigger represents one named host identity. `MessagingHost` selects
contracts only; it never selects handlers. The generated descriptor is
validated against SimpleInjector registrations at startup for normal message
and event handlers. The same event may be subscribed by multiple hosts because
each subscription forwards a separate Service Bus copy into its host's
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
not part of `MessagingHost` metadata. If the dispatcher cannot resolve
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

## 8. Service Bus resource lifecycle

Queues and topics may be provisioned by IaC. The Functions startup extension
must also be able to ensure declared resources exist, because Rebus currently
supports startup creation and this is useful for local/test deployments.

Event subscriptions are host-managed:

- the generated manifest describes the desired topics and subscriptions;
- startup creates the named host identity queue;
- **either** the owning publisher **or** any subscriber may `Ensure` a
  topic declared by the network (create if missing, never delete, never
  change foreign settings). This removes publisher-first deploy coupling;
- startup creates missing forwarding subscriptions to the host identity
  queue;
- startup **deletes obsolete subscriptions owned by that host, including
  in production**. Topic consumers are not rolled with dual subscription
  sets: an old subscription left in place would keep delivering events
  the host no longer handles and spike the DLQ. There is no rolling-upgrade
  grace period;
- subscription names are deterministic but are not public contract API;
- operations use Service Bus management APIs and are concurrency-safe when
  multiple instances of the same or different hosts start concurrently;
- deletion is restricted to subscriptions demonstrably owned by the host
  identity. Queues and topics are never auto-deleted.

The implementation must account for Azure Service Bus naming and management
limitations before selecting the deterministic name. A host may subscribe to
multiple topics and different hosts may subscribe to the same topic safely.

Rolling topology changes are an accepted deployment risk in both directions.
Removing a subscription can race with an old host version that still expects
it; adding a subscription can deliver a new event to an old host version that
cannot process it. The framework does not attempt version-coordinated
subscription sets. Deployments must stop/drain incompatible old processors or
use versioned host identities/contracts when zero-overlap rollout is required.

## 9. Restricted bus shim

The Functions composition registers a restricted `IBus` implementation. Its
public operations are one-way:

```text
Send<T>(T message)
Send<T>(T message, TimeSpan delay)
Send<T>(T message, DateTimeOffset dueTime)
Publish<T>(T event)
```

The shim:

- validates that sent messages have an explicit owner queue;
- routes events to `<owner-publisher>-<contract-name>`;
- permits `Publish` only when the network declares `PubSub` and the current
  named host identity equals the event's owner publisher;
- permits delayed `Send` only when the network declares `ScheduledSend`;
- selects and applies the write protocol;
- creates `amf1-*` type/correlation/message headers;
- accepts additional caller headers while rejecting reserved-header overrides;
- delegates delivery, scheduling, and publishing to the composed transport
  (Service Bus native scheduling, Storage Queue visibility delay, InMemory
  scheduling);
- runs outgoing pipeline steps before serialization and transport send;
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
processing. Both are opt-in per network and are resolved from the shared
network pipeline registration.

Additional headers supplied to `Send` or `Publish` flow through outgoing steps.
Reserved routing, content, compression, attachment, and identity headers
cannot be overridden; the API rejects such input explicitly.

## 11. DataBus claim-check

The runtime exposes a shared DataBus abstraction equivalent to Rebus
`IDataBus`, `DataBusAttachment`, and claim-check steps. The network declares
only transport-facing offload thresholds and integrity bounds. The concrete
provider/store and provider-specific minimum attachment lifetime are runtime
composition decisions, exactly like the transport: startup composes a
provider, and that all hosts on one network compose the same provider, store,
and compatible options is a documented deployment assumption validated per
host, not cross-host.

Sending serializes first, optionally compresses, then compares the resulting
bytes with the network's configured maximum payload threshold. If the payload is
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
   larger value that also covers entity TTL, backlog, host outages, and
   deployment delays; the framework cannot prove those external bounds.

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

1. **Publisher host:** a producer-only participant (`Role = Producer`) hosted
   outside Azure Functions (for example the Minimal API web host); it owns
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
native bus and failed-message API. Outbox selection follows the registered bus
backend, not merely the presence of the framework `IBus` abstraction:

| Registered `IBus` backend | Outbox composition |
| --- | --- |
| Rebus adapter | Existing `Ark.Tools.Outbox.Rebus` durable outbox |
| Native Mediator Framework network bus | Non-durable passthrough outbox |

The existing sample `WebInterface` remains a Rebus-backed sender and keeps the
real Rebus outbox with its processor disabled. The existing
`RebusProcessor` remains Rebus-backed and keeps the real Rebus outbox with its
processor enabled. Their registrations and behavior must not be replaced by
the passthrough implementation.

Only a host that registers the native Mediator Framework network bus registers
the passthrough outbox. It buffers application sends until database commit and
then sends directly through that bus. This passthrough is not durable outbox
support: commit happens before send, so a send failure can leave committed
state without a dispatched message and must be documented and tested.

No durable outbox is introduced by the Functions messaging feature. No Rebus
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
- A shared transport-contract conformance suite covers send, scheduled send,
  publish/forwarding, PeekLock settlement, delivery count, DLQ, and lock
  expiry. It runs fully against the InMemory transport and, where
  infrastructure permits, against Azure transports.
- Runtime dispatch, settlement, scope, retry, second-level, and scheduling
  tests run against the first-class InMemory transport.
- Resource lifecycle tests run concurrent host-start and host-update
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
- Boundary tests use the real Functions host where Core Tools and a usable
  Service Bus infrastructure are available; absence is explicit, never a
  silent skip.
- The sample test proves three-host publish/subscribe behavior and distinct
  handlers.
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
| Compression changes payload bytes | Carry standard content-encoding and claim-check final compressed bytes |
| DataBus attachment deleted during retry | Use provider lifecycle cleanup, never consumer deletion |
| Custom propagation diverges between hosts | Use one opt-in transport pipeline and shared step contracts |
| Unsupported protocol in an old message | Direct fail-fast DLQ with explicit diagnostics |
| Second-level handler fails at exhaustion | Fail-fast → DLQ; otherwise abandon. Next deliveries are normal `T` until `2N` |
| Service Bus cannot delay abandon | Immediate `AbandonMessageAsync`; retry storm accepted. `RetryDelay` applies only to Storage Queue `visibilityTimeout` |
| QueueTrigger success-delete after SDK poison | AZM-11 verifies host delete-miss is benign; otherwise change only the immediate-DLQ path |
| Storage Queue poison move is non-transactional | Duplicate poison copies are accepted; preserve the original message ID and document consumer-side deduplication when required |
| Database commit succeeds but passthrough send fails | Document the non-durable gap; surface the send error and keep durable outbox support out of scope |
| Rolling subscription additions/removals | Accepted deployment risk: both adding and deleting subscriptions can be incompatible with old processors; stop/drain or version identities for incompatible rollouts |
| Duplicate processing during settlement | Complete only after handler success; document at-least-once semantics |
| InMemory semantics drift from Azure transports | One shared transport-contract conformance suite runs against every transport |
| Composed transport lacks a declared capability | Startup validation fails with an explicit capability diagnostic |
| Composed transport diverges from the generated Functions trigger binding | Manifest records the trigger selection; startup fails naming both |
| Functions runtime poisons Storage Queue messages before framework exhaustion | Visible `host.json` `maxDequeueCount` contract, generator diagnostic, startup warning with strict opt-in |
| Blob attachment expires while a message remains deliverable | Provider-specific minimum lifetime plus IaC lifecycle policy; operators include TTL, backlog, outages, scheduling, and retry windows |
| Host connected to a different network type | `amf1-network` fails fast. Same-type wrong namespace is undetectable; DataBus mismatches fail on length/hash |
| Message/event API confusion | Separate attributes, diagnostics, and routing rules |
| Rebus semantic drift | Reuse header names and failure concepts without coupling the new runtime to Rebus |
