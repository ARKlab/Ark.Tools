# AMF-01 — Send/receive seam split: pull message source

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AZM-05, AZM-18
**Scope**: FRAMEWORK
**Design**: [Solution shape](../../../messaging-throughput-prd.md#5-solution-shape), [Breaking changes](../../../messaging-throughput-prd.md#11-breaking-changes)

## Problem

`IMessagingReceiveTransport.ReceiveAsync` returns
`IAsyncEnumerable<IMessagingLockedDelivery>`. That single shape cannot express a
batch, cannot carry credits, cannot report "the queue is empty", and cannot
declare that a transport already owns its own pump. Every throughput feature in
the PRD is blocked on it.

The seam also conflates sending with processing. Sending (`IMessagingTransport`,
`IBus`) is used by every host including Azure Functions and must not change;
processing is host-specific and is what needs replacing.

## Execution map

- **Send seam untouched**: `IMessagingTransport` send/publish/schedule/sizing
  members keep their exact signatures. No producer, and no Azure Functions host,
  changes.
- **Pull seam**: `IMessagingMessageSource` with `ReceiveBatchAsync(queue,
  maxMessages, maxWait, ctk)` returning zero-or-more deliveries, where zero means
  "empty" rather than "ended".
- **No push seam**: every transport is a pull source. Azure Functions receivers
  never reach a framework seam, and Service Bus is a pull source too
  ([why](../../../messaging-throughput-prd.md#154-rejected-servicebusprocessor-as-the-service-bus-pump)).
- **Capability record**: `MessagingReceiverCapabilities` (maximum batch size,
  server-side wait support, lock renewal support, native lock duration) so the
  host can validate and adapt instead of guessing.
- **Delivery contract**: `IMessagingLockedDelivery` gains required `LockedUntil`
  and `DeliveryId` members.
- **Removal**: `IMessagingReceiveTransport` and `MessagingReceivePump` are
  deleted. Messaging is pre-release; no adapter, no obsolete shim, no default
  interface implementation.
- **Boundary**: this task ships the contracts plus the in-memory implementation
  and mechanical call-site updates only. The processor host is AMF-02.

## Implementation steps

1. Add `IMessagingMessageSource`, `MessagingReceiverCapabilities` and
   `MessagingProcessingOptions` to `Ark.Tools.MediatorFramework.Messaging`.
2. Specify the empty-result contract precisely: `ReceiveBatchAsync` returns an
   empty list after at most `maxWait`, never throws for an empty queue, and never
   returns more than `maxMessages`.
3. Add `LockedUntil` and `DeliveryId` to `IMessagingLockedDelivery` and implement
   them on every existing delivery type.
4. Delete `IMessagingReceiveTransport` and `MessagingReceivePump`, and update the
   composition path to compose the message source.
5. Implement `IMessagingMessageSource` on the in-memory transport with honest
   batching and empty results, backed by an injectable clock.
6. Implement the seam on the Storage Queue and Service Bus transports at
   `MaximumBatchSize` parity with today (batching lands in AMF-06/AMF-07) so the
   solution stays green.
7. Fail composition with a named diagnostic when a transport implements no
   message source, or when a processor host is composed in a host that owns
   triggering (Azure Functions).
8. Update the API surface baseline and any generated snapshots.

## Core code shapes

`ReceiveBatchAsync` owns the wait; it does not sleep on empty. Deciding what to
do with an empty batch belongs to the host (AMF-03), which is why the transport
must not contain a hard-coded delay.

`MessagingReceiverCapabilities` is read once at composition time and is the only
place transport-specific limits enter the runtime.

## Guide contribution

Update the messaging transport guide with the send-versus-process split, the
pull receive seam, the capability record, the empty-batch contract, and an
explicit statement that Azure Functions receivers are unaffected.

## Sample extension

Update `Ark.MediatorFramework.Sample` composition to the new seam. Behaviour is
unchanged at this task; only the wiring moves.

## Required test coverage

- An empty queue returns an empty batch within `maxWait` and does not throw.
- `maxMessages` is never exceeded, including when the broker returns more.
- `LockedUntil` and `DeliveryId` are populated by every transport.
- Capabilities reported by each transport match its real behaviour.
- Composing a processor host in a Functions host fails startup with the named
  diagnostic.
- Existing settlement, retry and scoping tests still pass through the new seam.

## Outcomes

- Sending and processing are separate contracts.
- The receive seam can express batches, credits and emptiness.
- Transports declare their capabilities instead of the host assuming them.

## Acceptance

- [ ] Pull seam, capability record and processing options are public and documented.
- [ ] `LockedUntil` and `DeliveryId` are required and implemented everywhere.
- [ ] `IMessagingReceiveTransport` and `MessagingReceivePump` are removed with no adapter.
- [ ] In-memory, Storage Queue and Service Bus transports implement the new seam.
- [ ] API surface baseline is updated.
- [ ] The [task board](../README.md) status for AMF-01 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
