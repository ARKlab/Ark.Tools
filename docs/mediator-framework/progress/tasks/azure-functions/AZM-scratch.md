# AZM scratch — future refinements

Unscheduled notes for future AZM tasks. Items here are not yet assigned to a
task.

## API shape

- **Rename `MessagingCapabilities.Receive` to `SendReceive`** so the flag name
  reflects that the capability covers both inbound and outbound point-to-point
  delivery, not just reception.

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

## Serialization

- **Validate MessagePack and Protobuf decorations at compile time.** When a
  participant declares `SerializationProtocol.MessagePack` or
  `SerializationProtocol.Protobuf` in its `Serializers`, the analyzer should
  verify that every contract the participant processes, publishes, or subscribes
  to carries the matching wire-format attribute (e.g., `[MessagePackObject]` for
  MessagePack, `[ProtoContract]` for Protobuf). Missing attributes should produce
  a compile-time error diagnostic.
