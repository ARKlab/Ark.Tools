# AZM scratch — reviewed refinements

Review date: 2026-08-29.

The Mediator Framework has not been released. A **pre-release** item is a
contract, wire-format, topology, or primary-composition decision that would be
needlessly breaking after the first release. A **future** item is either not
needed or can be added without breaking the released contract.

Open questions below require an explicit decision before their related
pre-release work is specified.

## API shape

### Generic declaration attributes

**Timing: pre-release.**

Replace only the host, network, and participant declaration attributes with
generic attributes. Contract lists, pipeline-step lists, and the participant
retry policy remain type-valued attribute properties. Generic attributes do not
change the existing requirement for generated declaration classes to be
partial. Remove the corresponding non-generic attributes before release rather
than supporting two declaration syntaxes.

Open question: what are the exact declaration interfaces and minimum
static-abstract members for hosts, networks, and participants?

### `IBus` registration and setup

**Timing: pre-release.**

Registration is currently split across transport, codec, DataBus, participant,
bus, lifecycle, outbox, and Functions extensions. This does not fully realize
AZM-13's intended single discoverable composition entry point. Settle the
canonical composition model before release. The builder replaces the current
low-level public setup surface.

Use one root builder for each hosting mode — Functions receiver, custom
receiver, and producer-only host — with common naming and shared sub-builders
where their concerns overlap.

Decisions:

- `IServiceCollection` is canonical for integration with an outside native
  host, including ASP.NET Core, gRPC, Azure Functions, and transport
  infrastructure.
- Host-independent application concerns remain in SimpleInjector.

Open questions:

- Does one composition root support multiple networks or participants?
- Which choices belong to the builder: transport, DataBus, codecs, pipelines,
  lifecycle, and outbox?

### Multiline messaging `ArkApiSurface.txt` entries

**Timing: pre-release.**

The current one-line messaging records make the API-surface feature too
difficult to use: any change replaces an entire dense line, obscuring the field
that developers must review. Change the emitted and accepted grammar before
release. This is a soft breaking change because existing baselines fail and
developers must explicitly review and accept the clearer generated diff.

Before scheduling, define block delimiters, field ordering, ownership of
diagnostics, and whether only set-valued fields or every field receives its own
line.

### Human-readable transport-neutral names

**Timing: pre-release.**
**Assigned:** [AZM-17](AZM-17-logical-names-and-native-entity-mapping.md).

The common model currently forces contract names to lowercase snake case and
participant identities to portable queue syntax. That leaks native entity-name
constraints into logical names. Correct the layering now: wire headers and
registries should use stable logical names, while each transport maps logical
entity names to its native restrictions. Changing this later would alter wire
identities and deployed topology.

Decisions:

- Contract, participant, network, topic, and subscription names are logical.
- Logical names are lowercase and may contain `-`, `_`, `.`, and `/`
  separators.
- `amf1-msg-type` always contains the logical contract name.
- InMemory uses the logical entity name unchanged.
- A native transport preserves characters it supports. If replacement or
  truncation is required, it appends a stable hash of the complete logical name
  to the readable prefix. This prevents two logical names from collapsing to
  one native entity; generation reports a diagnostic if the final names still
  collide or cannot fit the native limit.
- Topics are first derived from the logical publisher and current logical
  contract name, then mapped once by the transport. `FormerNames` remain
  receive-time aliases for `amf1-msg-type`; they do not create or alias native
  topics. Changing the current contract or publisher name therefore still
  requires an explicit topology migration.

### Transport payload sizing

**Timing: pre-release contract finalization.**
**Assigned:** [AZM-18](AZM-18-transport-contract-and-servicebus-topology.md).

Replace the current model:

1. The compression threshold remains statically declared.
2. The transport contract exposes its maximum payload size in bytes through a
   static interface member; the value is fixed for each transport type and used
   by runtime composition.
3. "Payload" means the complete headers plus body representation.
4. The transport provides only its native header-size computation.
5. The runtime adds serialized body size to the transport-computed header size
   and transparently offloads the body to DataBus when the total exceeds the
   transport maximum. It recomputes the attachment-reference headers before the
   final limit check.

Remove `MeasureNative`, the network-level maximum transport payload, and the
network-level DataBus offload threshold. Compression and DataBus claim-check
remain transparent runtime concerns.

### `MessagingCapabilities.Receive` naming

**Timing: pre-release.**
**Assigned:** [AZM-18](AZM-18-transport-contract-and-servicebus-topology.md).

Rename `Receive` to `SendReceive`. Point-to-point `Send` requires a receiving
participant and is available only when this capability is declared. A network
without `SendReceive` is publish-only; it may publish events when `PubSub` is
declared.

### Scheduled send and current-message deferral

**Timing: pre-release for naming; future for current-message deferral unless a
required use case is identified.**
**Pre-release naming assigned:**
[AZM-18](AZM-18-transport-contract-and-servicebus-topology.md).

If Rebus terminology is the target API, rename the two delayed `Send` overloads
to `Defer` before release. Deferring the currently handled message is a separate
feature, not merely a rename. Existing retry policies cover immediate
redelivery but not "retry tomorrow." Without current-message deferral a
consumer must define and send itself a new contract instead of reusing the
current one. That is explicit and simple, but duplicates contracts, changes
message identity, and does not work for a subscription consumer that cannot
point-to-point send to itself. Current-message deferral remains relevant, but
may be postponed until its delivery guarantees are designed.

Open questions:

- Does "native deferral" mean scheduled re-enqueue? Service Bus deferred-message
  settlement is not scheduled delivery and does not reset delivery count.
- Must schedule-copy-and-complete be atomic? Without a broker transaction or
  outbox it can duplicate or lose a message.
- Which message ID and framework/application headers are preserved, and which
  delivery metadata is removed?

### Network maximum transport payload

**Timing: pre-release.**
**Assigned:** [AZM-18](AZM-18-transport-contract-and-servicebus-topology.md).

Remove this network setting and its default. Each transport declares its actual
complete payload limit at runtime. Compression runs from its static threshold;
body offload to DataBus happens transparently when the computed headers plus
body exceed the selected transport's limit.

## Network and host configuration

### Lifecycle and connection settings

**Timing: pre-release.**

Move deployment/host concerns out of `MessagingNetworkAttribute` and
`MessagingNetworkOptions`. `ConnectionConfigurationKey` already has a
Functions-host override. Put effective connection and lifecycle settings on a
host/composition model so the same logical network can be hosted differently
without changing its contract declaration. Remove
`ManagedIdentityConfigurationKey` unless it represents configuration that
cannot live below the connection prefix.

Open questions:

- Must lifecycle be identical across all hosts of one deployed network, or may
  one reconciler use `CreateIfMissing` while other hosts use `External`?
- What is the equivalent host-local model for Rebus and non-Functions
  composition?
- What distinct value does `ManagedIdentityConfigurationKey` carry beyond the
  existing connection-prefix `clientId` convention?

### Service Bus subscription forwarding

**Timing: pre-release topology decision.**
**Assigned:** [AZM-18](AZM-18-transport-contract-and-servicebus-topology.md).

The single participant queue and single trigger are implementation
conveniences, not requirements. Prefer direct Service Bus subscription triggers:
they remove a broker hop and retain locking, delivery count, and DLQ behavior on
the subscription itself. Generate a command-queue trigger plus one trigger per
subscription. All triggers for one participant must share its concurrency
budget and retry/DLQ policy through participant-level runtime configuration; a
single generated trigger is not required. No total ordering across independent
entities is promised. Changing this later would break manifests, generated
triggers, lifecycle reconciliation, and deployed resources.

## Serialization

### MCP `ProblemDetails` JSON options

**Timing: pre-release decision.**

The MCP adapter currently owns a source-generated serializer context while
other host surfaces use host-configured JSON behavior. Settle the ownership
before release. If host options are used, preserve the current sanitized error
copy and ensure arbitrary extension values cannot bypass the safe error
boundary or fail unexpectedly under AOT.

Open questions:

- Which options are authoritative: ASP.NET Core `HttpJsonOptions`, MVC
  `JsonOptions`, or dedicated MCP `JsonSerializerOptions`?
- MCP remains source-generation/AOT-only.
- How are custom `ProblemDetails.Extensions` values constrained or registered
  for serialization?

### MessagePack and Protobuf compile-time validation

**Timing: pre-release.**
**Assigned:** [AZM-19](AZM-19-non-json-contract-validation.md).

Add analyzer coverage before release so declared protocols do not defer contract
failures to startup. This is contract-level topology validation, not validation
of host serializer options:

- A participant publishing an event with MessagePack as its effective wire
  serializer requires `[MessagePackObject]` on that event, and every subscriber
  must declare MessagePack support.
- A participant processing a message with MessagePack as its effective wire
  serializer requires `[MessagePackObject]` on that message.
- Apply the equivalent contract-shape check to each other effective non-JSON
  wire serializer. For the shipped protobuf codec this means the Google.Protobuf
  contract shape, not protobuf-net's `[ProtoContract]`.
- Continue validating publisher/subscriber protocol compatibility.

Custom resolver and parser registration remain host startup concerns and are
not analyzed.

## Observability

### OpenTelemetry activities

**Timing: pre-release for the semantic baseline; future for incremental
enrichment.**

The runtime already creates a consumer activity and propagates outgoing trace
context, but it does not create producer send/publish activities. Establish
stable activity names, producer/consumer boundaries, trace-state propagation,
messaging attributes, and settlement-based status before the first release.
Follow the latest stable OpenTelemetry messaging semantic conventions available
when implementing the task. Activities are emitted by default; registering an
OpenTelemetry listener/exporter is opt-in. Network, participant, destination,
and contract names are all acceptable attributes.

Dispatch child spans, retry/settlement events, fan-out links, batch links, and
broader framework instrumentation are incremental future improvements.

Open question: does final settlement determine consumer activity status?

### OpenTelemetry metrics

**Timing: pre-release for the metric contract; future for additional
instruments.**
**Pre-release metric contract assigned:**
[AZM-20](AZM-20-opentelemetry-messaging-metrics.md).

The runtime currently exposes two custom histograms for processing time and
successful queue time. Finalize their names, units, descriptions, outcome
semantics, and attributes before release, following the latest stable
OpenTelemetry messaging metrics conventions available at implementation time.
Metrics are recorded by default; OpenTelemetry collection/export remains
opt-in.

The pre-release baseline should cover:

- send, publish, and process duration;
- time in queue when a valid sent timestamp is available;
- processed-message outcomes based on final settlement;
- delivery attempt count.

Network, participant, destination, and contract names are acceptable metric
attributes. Exception messages, message IDs, correlation IDs, and attachment
IDs are not metric attributes. Additional payload-size, compression, DataBus,
retry, dead-letter, and active-operation instruments are future incremental
improvements after concrete operational questions and aggregation behavior are
defined.
