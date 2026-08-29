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

**Timing: pre-release, if approved.**

Replacing type-valued attributes is source-breaking. The current network,
participant, Functions host, and Rebus host APIs expose participant, contract,
retry-policy, and pipeline-step types through `Type` values. If declaration
interfaces with static-abstract members are the intended model, introduce them
before the first release rather than supporting two declaration models later.

Open questions:

- Does this apply only to host-to-participant/network references, or also to the
  variable-length `Members`, `Processes`, `Publishes`, `Subscribes`,
  `IncomingSteps`, and `OutgoingSteps` lists?
- Is `Retry` also a generic type argument?
- What are the exact declaration interfaces and required static-abstract
  members? Are declaration classes still required to be `sealed partial`?
- Should the existing non-generic attributes be removed or retained as a
  compatibility syntax?

### `IBus` registration and setup

**Timing: pre-release for the canonical API; future for an additive facade.**

Registration is currently split across transport, codec, DataBus, participant,
bus, lifecycle, outbox, and Functions extensions. This does not fully realize
AZM-13's intended single discoverable composition entry point. Settle the
canonical composition model before release. A builder that only wraps and
retains all current public registration methods is incremental and may wait.

Open questions:

- Does the builder replace the low-level extensions or only provide a preferred
  facade over them?
- Must one model cover Functions receivers, custom receivers, and producer-only
  hosts?
- Does one composition root support multiple networks or participants?
- Which choices belong to the builder: transport, DataBus, codecs, pipelines,
  lifecycle, and outbox?
- Is `IServiceCollection` the canonical composition API, with SimpleInjector
  integration layered on top?

### Multiline messaging `ArkApiSurface.txt` entries

**Timing: future.**

This is diff-readability tooling, not a runtime or public API correction. The
one-line grammar is deterministic and already enforced. A later change can
remain non-breaking by accepting old one-line baselines while emitting a
versioned block format during an explicit baseline migration.

Before scheduling, define block delimiters, field ordering, ownership of
diagnostics, and whether only set-valued fields or every field receives its own
line.

### Human-readable transport-neutral names

**Timing: pre-release.**

The common model currently forces contract names to lowercase snake case and
participant identities to portable queue syntax. That leaks native entity-name
constraints into logical names. Correct the layering now: wire headers and
registries should use stable logical names, while each transport maps logical
entity names to its native restrictions. Changing this later would alter wire
identities and deployed topology.

Open questions:

- Which values are logical names: contracts, participants, networks, topics,
  and subscriptions?
- Are case and all separators preserved, or is only `.` added to the accepted
  canonical syntax?
- What deterministic transport mapping and collision diagnostic apply?
- Does `amf1-msg-type` always retain the logical name?
- How do `FormerNames` and the current
  `<publisher-identity>-<contract-name>` topic derivation interact with native
  normalization?

### `IMessagingTransport.MeasureNative`

**Timing: pre-release contract finalization.**

Keep the capability. Claim-check decisions must include headers and native
encoding; Storage Queue Base64 and Service Bus application properties cannot be
derived from payload length alone. Rename it before release to a precise name
such as `MeasureInlineEnvelopeBytes` and document whether the result is exact or
a conservative upper bound.

Open question: is `MaximumTransportPayloadBytes` a payload-only policy
threshold, while `MaximumInlineEnvelopeBytes` plus native measurement is the
hard complete-envelope limit? The implementation currently treats them that
way, but the public terminology does not make the distinction clear.

### `MessagingCapabilities.Receive` naming

**Timing: future — reject the proposed rename.**

Keep `Receive`. Sending is an implicit operation supported by every transport;
`Receive` is the optional capability backed by `IMessagingReceiveTransport`.
`SendReceive` would imply that send is unavailable without the flag and is less
accurate than the current name. Do not retain this as future implementation
debt unless the capability model itself changes.

### Scheduled send and current-message deferral

**Timing: pre-release for naming; future for current-message deferral unless a
required use case is identified.**

If Rebus terminology is the target API, rename the two delayed `Send` overloads
to `Defer` before release. Deferring the currently handled message is a separate
feature, not merely a rename. Existing participant retry policies already cover
handler retries, so this feature is not currently required and can be added
incrementally later after its delivery guarantees are designed.

Open questions:

- What use case is not covered by the existing retry policy?
- Does "native deferral" mean scheduled re-enqueue? Service Bus deferred-message
  settlement is not scheduled delivery and does not reset delivery count.
- Must schedule-copy-and-complete be atomic? Without a broker transaction or
  outbox it can duplicate or lose a message.
- Which message ID and framework/application headers are preserved, and which
  delivery metadata is removed?

### Default maximum transport payload

**Timing: pre-release.**

Lower the default before release, but do not use `50,000` as a claimed
cross-transport-safe value. Storage Queue's documented canonical inline limit
is `46,080` bytes before Base64, while Service Bus separately enforces a
256-KiB complete-message ceiling. Choose at most `46,080` for a portable
payload policy, and keep native envelope measurement as the final hard-limit
check.

Open question: should the default optimize portability (`46,080`) or remain a
payload-only policy independent of transport, requiring Storage Queue networks
to override it explicitly?

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

Prefer direct Service Bus subscription triggers unless the single participant
identity queue is an intentional cross-transport invariant. Direct triggers
remove a broker hop and retain locking, delivery count, retry limits, and DLQ
behavior on the subscription itself. Keeping forwarding is defensible only if
one mixed command/event queue, one trigger, or cross-transport ordering is a
required semantic guarantee. Changing later would break manifests, generated
triggers, lifecycle reconciliation, and deployed resources.

Open questions:

- Is one physical participant queue a required portability or ordering
  guarantee, or an implementation convenience?
- Must directly addressed messages and subscribed events share ordering,
  concurrency, retry, and DLQ policy?
- Is one generated trigger per participant a hard Functions-host requirement?

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
- Must MCP remain source-generation/AOT-only?
- How are custom `ProblemDetails.Extensions` values constrained or registered
  for serialization?

### MessagePack and Protobuf compile-time validation

**Timing: pre-release, with corrected requirements.**

Add analyzer coverage before release so declared protocols do not defer contract
failures to startup. Do not universally require `[MessagePackObject]`: the
runtime supports host-supplied MessagePack resolvers. Do not check
`[ProtoContract]`: the shipped codec uses Google.Protobuf and expects
`IMessage` plus a registered parser. Validation must follow the actual resolver
and parser model and the contract's read/write direction.

Open questions:

- Is attribute-based MessagePack the only supported policy, or are custom
  resolvers first-class?
- Is Google.Protobuf the sole supported protobuf model?
- How does the analyzer recognize custom formatters and registered
  `MessageParser<T>` instances?
- Must every participant contract support every listed serializer, or only the
  effective wire protocol used for that route?

## Observability

### OpenTelemetry activities

**Timing: pre-release for the semantic baseline; future for incremental
enrichment.**

The runtime already creates a consumer activity and propagates outgoing trace
context, but it does not create producer send/publish activities. Establish
stable activity names, producer/consumer boundaries, trace-state propagation,
low-cardinality messaging attributes, and settlement-based status before the
first release. Dispatch child spans, retry/settlement events, fan-out links,
batch links, and broader framework instrumentation are incremental future
improvements.

Open questions:

- Which OpenTelemetry messaging semantic-convention version is the baseline?
- Are instrumentation steps opt-in or registered by default?
- Does final settlement determine consumer activity status?
- Which destination/network/participant values are safe as bounded-cardinality
  tags?
