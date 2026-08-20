# AZM-08 — Restricted `IBus` shim

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-02, AZM-04, AZM-05, AZM-06, AZM-07
**Scope**: RUNTIME
**Design**: [Restricted bus shim](../../azure-functions-messaging-design.md#9-restricted-bus-shim)

## Problem

Handlers need to send messages and publish events without knowing transport
types, while the bus must not expose request/reply or receive semantics. The
bus targets the AZM-05 transport contract, so its behavior is identical over
InMemory, Service Bus, and Storage Queue. It is a shim because it preserves the
small Rebus-like one-way surface application handlers need, allowing an easy
composition switch, while intentionally reducing the framework API surface.

## Execution map

- **Public API**: define the restricted framework `IBus` in
  `Ark.Tools.MediatorFramework`; include cancellation tokens and an optional
  `Dictionary<string, string>` of additional application headers on every
  operation.
- **Native implementation**: implement one transport-neutral bus in
  the `Ark.MediatorFramework.Messaging` namespace of `Ark.Tools.MediatorFramework` composed from the message-context,
  pipeline, DataBus, and transport seams of AZM-04/05/06/07. There is no
  per-technology bus implementation.
- **Routing source**: use only the generated registry (contract → owning
  participant → route); callers never pass queue/topic names.
- **Capability guards**: delayed `Send` requires the network to declare
  `ScheduledSend`; `Publish` requires `PubSub` plus the current participant
  declaring the event in `Publishes`. Violations throw with the capability or
  participant named in the message.
- **Runnable state**: at task end the bus sends, schedules, and publishes over
  the InMemory transport end-to-end; full solution builds and tests green.
- **Stop condition**: do not add receive, request/reply, `SendLocal`, worker,
  or outbox methods. Azure transports arrive in AZM-10/AZM-11 without changing
  this bus.

## Implementation steps

1. Define the Mediator Framework transport-neutral restricted one-way `IBus`
   contract for `Send`, `Publish`, and delayed `Send` variants using both
   `TimeSpan` and `DateTimeOffset`. Do not expose delayed `Publish`.
2. Implement sending to the processing participant's identity queue through
   the composed transport, resolved through the generated registry. Sending a
   message no member processes (unwired) fails explicitly.
3. Implement publishing to `<publisher-identity>-<contract-name>` through the
   composed transport when `PubSub` is declared and the current participant
   declares the event in `Publishes`.
4. Enforce the capability guards defined in the Execution map, plus
   negative/past delay validation and network scheduling limits before
   enqueue. No API cancels an already scheduled message.
5. Reject all local-send/request/reply operations by omission; the interface
   has no such members.
6. Delegate header construction, body serialization, content encoding, compression, and
   DataBus claim-checking to AZM-04/AZM-07.
   The generated registry binds every `Send<TMessage>`/`Publish<TEvent>` to its
   generic serializer and logical name; it never resolves a payload CLR type
   from runtime metadata. This mirrors generated Minimal API and HttpTrigger
   response serialization.
7. Propagate message, correlation, causation, sent-time, sender-identity, and
   allowed context headers using centralized constants. Write
   `amf1-sender-identity` for both `Send` and `Publish`.
8. Run outgoing pipeline steps before serialization and transport send.
9. Ensure the bus is disposable, safe for concurrent invocations, and does not
   start a receive worker.
10. Bound caller header count/key/value sizes and reserve framework routing,
    serialization, DataBus, trace, and user-context headers.

`Publish<TEvent>` requires the sending participant to declare the event in its
`Publishes` set. A sender-only participant (owning no contracts) may still use
`Send<TMessage>` to the processing participant's identity queue; it cannot
publish.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
restricted `IBus` API, owner routing, additional headers, send-only
scheduling, and the capability guard behavior.

## Sample extension

Extend the Book sample sender composition to send Book background messages
through the framework bus over the InMemory transport, with a passing
send-and-inspect fixture (receive dispatch arrives in AZM-09).

## Required test coverage

- Messages route to the processing participant's identity queue.
- Per-publisher/per-contract topic routing for events.
- Publish from a participant declaring the event in `Publishes`, including
  a publisher-only participant with no receive registration.
- Sender-only participants reject `Publish` but allow `Send`.
- Delayed `Send` on a network without `ScheduledSend` throws naming the
  capability; `Publish` on a network without `PubSub` throws naming the
  capability.
- JSON, MessagePack, and protobuf sends.
- Additional headers are accepted and reserved headers are rejected.
- Every overload accepts optional additional headers and writes the original
  sending participant identity.
- Scheduled send via the transport contract with a controlled clock.
- Compression and DataBus offload are applied before send using the effective
  limit (smaller of network threshold and transport ceiling).
- Missing processor/publisher (unwired contract) rejection.
- No request/reply, local send, or receive API is available.
- Disposal and cancellation do not leave partial sends.

## Outcomes

- Application handlers use one restricted one-way bus abstraction.
- The same bus code serves every transport; capabilities gate behavior.
- Messages sent by the bus can be consumed by any receive-capable host on the
  same network.

## Acceptance

- [ ] Restricted bus API is public, documented, and contains no receive or
  reply operation.
- [ ] Queue/topic routing, scheduling, and capability guards are tested over
  InMemory.
- [ ] Serialization and headers are delegated to the shared messaging runtime.
- [ ] Invalid routing and capability violations fail explicitly.
- [ ] No worker or processor starts during bus composition.
- [ ] The [task board](../README.md) status for AZM-08 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
