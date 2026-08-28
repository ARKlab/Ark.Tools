# AZM scratch — future refinements

Unscheduled notes for future AZM tasks. Items here are not yet assigned to a
task.

## API shape

- **Replace type-valued Mediator Framework attributes with generic attributes.**
  Constrain network, participant, and host type parameters to sealed declaration
  interfaces containing only the required static-abstract members.

- **Revisit the complete `IBus` registration and setup design.**
  Evaluate a builder-based composition model to make network, transport, DataBus,
  participant, and pipeline setup easier to discover and configure.

- **Keep common messaging names human-readable and transport-neutral.**
  Relax common framework naming rules so logical names can retain separators such
  as `.`; each transport should normalize names only where its native restrictions
  require it.

- **Revisit the `IMessagingTransport.MeasureNative` method.**
  Confirm whether the transport contract needs to expose native byte-limit measurement.

- **Rename `MessagingCapabilities.Receive` to `SendReceive`** so the flag name
  reflects that the capability covers both inbound and outbound point-to-point
  delivery, not just reception.

- **Rename scheduled send on `IBus` to `Defer`.**
  Align the transport-neutral API with Rebus terminology. Also let a host defer
  the current message for deferred retries: use native deferral when supported
  and reset its delivery count, otherwise schedule an identical payload and
  headers to the host's own queue and complete the current delivery.

- **Reduce `MaximumTransportPayloadBytes` default from 240,000 to 50,000** in
  `MessagingNetworkAttribute`. The 240 KB value is the Service Bus maximum; 50 KB
  is a safer default that fits Storage Queue and leaves room for envelope
  overhead. Hosts targeting Service Bus only can raise it explicitly.

## Network attribute

- **Move `ResourceLifecycle`, `ConnectionConfigurationKey`, and
  `ManagedIdentityConfigurationKey` from `MessagingNetworkAttribute` to the
  concrete host attribute** (e.g., the Azure Service Bus host attribute introduced
  in AZM-10). These properties are transport-specific and do not belong on the
  transport-neutral network declaration.

- **Revisit Service Bus subscription forwarding.**
  Re-evaluate forwarding every subscription into one participant identity queue.
  Service Bus subscriptions already provide queue-like delivery, locking, and
  dead-letter behavior, so the advantage of the extra queue remains to be proven.

## Serialization

- **Consider moving MCP error `ProblemDetails` serialization to host JSON options.**
  The current MCP adapter owns a source-generated serializer for its safe error
  payload. A future AZM integration should evaluate using the host's configured
  `JsonOptions` instead, so naming policies, converters, and other contract
  serialization settings are applied consistently. Preserve the sanitized
  error boundary when changing the serializer.

- **Validate MessagePack and Protobuf decorations at compile time.** When a
  participant declares `SerializationProtocol.MessagePack` or
  `SerializationProtocol.Protobuf` in its `Serializers`, the analyzer should
  verify that every contract the participant processes, publishes, or subscribes
  to carries the matching wire-format attribute (e.g., `[MessagePackObject]` for
  MessagePack, `[ProtoContract]` for Protobuf). Missing attributes should produce
  a compile-time error diagnostic.

## Observability

- **Analyze enhancing `Activity` usage for OTEL instrumentation.** Capture the
  future capability need: the messaging runtime (and framework in general)
  should be reviewed for richer OpenTelemetry instrumentation — proper
  `ActivitySource` activities with messaging semantic conventions, links,
  events, and status across send/publish/receive/dispatch. Needs analysis;
  not yet assigned to a task.
