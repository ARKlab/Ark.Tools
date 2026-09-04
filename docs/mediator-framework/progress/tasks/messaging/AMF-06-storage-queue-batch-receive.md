# AMF-06 — Storage Queues batch receive and pop-receipt race fix

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-01, AMF-02, AMF-03, AMF-04
**Scope**: FRAMEWORK
**Design**: [Transport profiles](../../../messaging-throughput-prd.md#7-transport-profiles), [Provider facts](../../../messaging-throughput-prd.md#44-provider-facts-that-constrain-the-design)

## Problem

`StorageQueueMessagingTransport` receives with `maxMessages: 1` although
`ReceiveMessagesAsync` returns up to **32** per call, and every call is a billed
transaction. This is the single largest throughput and cost win available: 32×
fewer receive transactions for the same message volume.

There is also a latent correctness bug. Storage Queues has no lock renewal, only
`UpdateMessage`, which **rotates the pop receipt**. `StorageQueueLockedDelivery`
mutates `_popReceipt` from renew and abandon with no synchronisation. It is
unreachable today only because everything is sequential; it becomes reachable the
moment the shared renewer (AMF-04) and a worker settle concurrently.

## Execution map

- **Batch receive**: `MaximumBatchSize = 32`, requesting `min(credit, 32)`.
- **Capabilities**: `SupportsServerSideWait = false` (no long poll),
  `SupportsLockRenewal = true` (via `UpdateMessage`), `NativeLockDuration` = the
  configured visibility timeout, `OwnsConcurrency = false`.
- **Pop-receipt safety**: the pop receipt becomes a single guarded value with a
  documented ordering rule — renew and settle can never interleave on one
  delivery, and settle always uses the newest receipt.
- **Visibility timeout**: derived from `MaximumHandlerDuration` plus the expected
  buffer wait rather than a fixed value, and validated at composition time.
- **Idle cost**: with AMF-03 the removed internal delay leaves the backoff in
  charge; document the resulting per-queue request rate.
- **Poison handling**: existing poison-queue behaviour and dequeue-count semantics
  are unchanged.

## Implementation steps

1. Report accurate `MessagingReceiverCapabilities` from the Storage Queue
   transport.
2. Request `min(credit, 32)` messages per call and map each returned message to a
   locked delivery with `LockedUntil` from `NextVisibleOn` and `DeliveryId` from
   the message id.
3. Replace the unsynchronised `_popReceipt` field with a guarded value, and make
   renew and settle mutually exclusive for a single delivery.
4. Ensure `DeleteMessage`/`UpdateMessage` always use the latest receipt, and that a
   stale-receipt failure is reported as lock lost rather than a generic error.
5. Derive and validate the visibility timeout from the handler duration and
   buffer wait.
6. Verify the batch path preserves per-message settlement decisions: one poison
   message in a batch of 32 must not affect the other 31.
7. Extend the conformance suite to cover batch receive, batch settlement and the
   renew-then-settle sequence.

## Core code shapes

One guarded pop receipt per delivery, written by renewal and read by settlement,
is enough — no lock hierarchy, no shared state across deliveries.

## Guide contribution

Document the batch size, the visibility-timeout derivation, the pop-receipt
rotation behaviour, the transaction-cost impact, and the absence of server-side
long polling.

## Sample extension

Run the sample's Storage Queue profile with batching enabled and record the
receive-transaction reduction in the sample readme.

## Required test coverage

- A batch of up to 32 is received in one call and never exceeds the credit.
- Renew rotates the receipt and a subsequent delete succeeds (the race).
- Concurrent renew and settle on one delivery cannot interleave.
- A stale receipt surfaces as lock lost with the existing settlement decision.
- Mixed outcomes within one batch settle independently.
- Poison-queue and dequeue-count behaviour is unchanged.
- Visibility timeout validation fails composition for impossible combinations.

## Outcomes

- Storage Queue receive transactions drop by up to 32× at the same throughput.
- The pop-receipt race is fixed before concurrency makes it reachable.

## Acceptance

- [ ] Batch receive up to 32 with correct credit clamping is implemented.
- [ ] Capabilities are accurate for Storage Queues.
- [ ] The pop-receipt race is fixed and covered by a conformance test.
- [ ] Visibility timeout is derived and validated.
- [ ] The [task board](../README.md) status for AMF-06 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
