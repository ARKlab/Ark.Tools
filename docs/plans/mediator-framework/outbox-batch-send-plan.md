# Outbox Batch Send Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `MessagingOutboxProcessor` sends each destination's messages of a poll through one batch call when the transport supports it, and Service Bus supports it.

**Architecture:** A new optional interface `IMessagingBatchTransport : IMessagingTransport`. The processor keeps its validation, grouping and cross-destination parallelism, and swaps only the per-destination loop for one `SendBatchAsync` call when the transport implements the interface. `ServiceBusMessagingTransport` packs a destination's messages into `ServiceBusMessageBatch`es in peek order, with `ScheduledEnqueueTime` set on scheduled ones.

**Tech Stack:** .NET 10, Azure.Messaging.ServiceBus, MSTest + AwesomeAssertions, the Service Bus emulator from `docker-compose.yml`.

**Spec:** [`docs/design/mediator-framework/outbox-batch-send.md`](../../design/mediator-framework/outbox-batch-send.md)

## Global Constraints

- No changed defaults: outbox `batchSize` stays `10` everywhere it is a parameter.
- Public API: one new interface plus its entry record; no new parameters.
- One `MessagingMetrics.RecordClientOperation` call per message, with that message's headers, operation (`send`, `defer`, `publish`) and destination.
- Any failure fails the whole poll; nothing is committed (unchanged).
- Repository rules from `AGENTS.md`: XML docs on public members, `async`/`await` (never return a `Task` directly), `ConfigureAwait(false)`, file header, file-scoped namespaces, `CultureInfo.InvariantCulture` in formatted messages, no new dependencies.
- Focused tests only: `dotnet test tests/Ark.Tools.MediatorFramework.Tests --filter ...`; the Service Bus tests need `docker compose up -d` and the emulator health check from `AGENTS.md`.

## Review Focus

1. A poll with messages for several destinations where one destination's batch call fails: the other destinations are cancelled and nothing is committed, the same as with single sends today. Test in Task 1.
2. A destination with more messages than fit one Service Bus batch: they arrive in peek order across several batches. Test in Task 2.
3. A queue that mixes immediate and scheduled messages in one poll: the immediate messages arrive at once, the scheduled ones only at their due time, and all headers survive. Test in Task 2.
4. A transport that doesn't implement the batch interface, such as Storage Queues, in-memory, or a wrapper: it behaves exactly as today. Covered by the existing `MessagingOutboxTests`, which must stay green unchanged.
5. A single message that passes `_validateSize` but doesn't fit an empty batch: it fails with an `InvalidOperationException` and doesn't loop forever. Test in Task 2.

---

### Task 1: Batch interface and processor dispatch

**Files:**
- Create: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/IMessagingBatchTransport.cs`
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/MessagingOutboxProcessor.cs` (`ProcessMessagesAsync`, `_sendAsync`)
- Test: `tests/Ark.Tools.MediatorFramework.Tests/MessagingOutboxTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct MessagingBatchEntry(IReadOnlyDictionary<string, string> Headers, ReadOnlySequence<byte> Payload, DateTimeOffset? DueTime);`
  - `public interface IMessagingBatchTransport : IMessagingTransport { Task<IReadOnlyList<TimeSpan>> SendBatchAsync(string destination, bool isTopic, IReadOnlyList<MessagingBatchEntry> messages, CancellationToken ctk); }`. It returns one duration per input message, in input order: the duration of the broker call that carried it. The processor passes `DueTime` only for queues; topics with a due time are already rejected.

- [ ] **Step 1: Write the failing tests** in `MessagingOutboxTests`, with a new private `BatchRecordingTransport : IMessagingBatchTransport` that records each `SendBatchAsync` call (destination, isTopic, entry ids from `MessagingHeaders.MessageId`) and can fail a named destination:
  - `ProcessorSendsEachDestinationAsOneBatchInPeekOrder`: seed `a1→queue-a`, `b1→queue-b`, `a2→queue-a` (`a2` with a due time), and a topic message `t1`. Assert one call per destination, ids `a1,a2` for `queue-a` with `a2`'s `DueTime` set, `isTopic` true only for the topic, `SendAsync`/`PublishAsync` never called, and the outbox empty afterwards.
  - `ProcessorBatchFailureOnOneDestinationCancelsOthersAndDoesNotCommit`: same shape as the existing `ProcessorFailureOnOneDestinationCancelsOthersAndDoesNotCommit`, but the batch transport fails `queue-a`. Assert the outbox count is unchanged.
  - `ProcessorRecordsOneClientOperationPerBatchedMessage`: with a `MeterListener` on `OpenTelemetryProcessingMetricsStep.MeterName` (pattern: `MessagingMetricsTests.Recorder`), seed 3 messages for one queue; assert 3 measurements on the client operation duration instrument, with values equal to the durations the fake returned.
- [ ] **Step 2: Run** `dotnet test tests/Ark.Tools.MediatorFramework.Tests --filter "FullyQualifiedName~MessagingOutboxTests"`. Expected: the new tests fail to compile or fail. The existing tests pass.
- [ ] **Step 3: Implement.** Add the interface and record with XML docs. In `ProcessMessagesAsync`, keep everything up to the grouping. Inside the `Parallel.ForEachAsync` body: when `_transport is IMessagingBatchTransport batch`, call `SendBatchAsync` once with the group's entries, then call `RecordClientOperation` per message with the returned durations. If the call throws, record each message with the elapsed time and rethrow, as `_sendAsync`'s `finally` does today. Otherwise keep the existing per-message loop.
- [ ] **Step 4: Run** the same filter. Expected: all `MessagingOutboxTests` pass. Then run `dotnet build` with 0 warnings.
- [ ] **Step 5: Commit** `feat(MediatorFramework): send outbox batches through batch-capable transports`.

### Task 2: Service Bus batch sends

**Files:**
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging.Azure/ServiceBusMessagingTransport.cs`
- Test: new `tests/Ark.Tools.MediatorFramework.Tests/ServiceBusMessagingBatchSendTests.cs` (`[TestCategory("integration")]`, `[DoNotParallelize]`). Reuse the connection-string and queue setup pattern of `ServiceBusMessagingTransportConformanceTests`, with its own queue name.
- Modify: `CHANGELOG.md` under `## [Unreleased]`

**Interfaces:**
- Consumes: `IMessagingBatchTransport`, `MessagingBatchEntry` from Task 1.
- Produces: `ServiceBusMessagingTransport : IMessagingBatchTransport`.

- [ ] **Step 1: Write the failing tests** against the emulator:
  - `SendBatchDeliversMessagesInOrderWithHeaders`: 5 entries with ids `m1..m5` and a custom header. Receive all 5 and assert the order `m1..m5`, headers and bodies equal, and returned durations count `5`.
  - `SendBatchSplitsAcrossServiceBusBatchesInOrder`: 12 entries of ~60 KB payload each, which forces several batches under the 256 KB limit. Assert all 12 are received in order.
  - `SendBatchSchedulesMessagesWithADueTime`: `m1` immediate and `m2` due 5 s ahead. Assert `m1` is received at once. Assert `m2` is not received within 1 s, and that `PeekMessagesAsync` shows it with `ScheduledEnqueueTime` equal to the due time (allow 1 s of tolerance).
  - `SendBatchRejectsAMessageThatCannotFitAnEmptyBatch`: an entry whose payload plus headers equals exactly `MaximumPayloadSizeBytes`. Assert `InvalidOperationException` and no infinite loop (use a test timeout). If `TryAddMessage` accepts it on the emulator, assert it is delivered instead, and note in the PR that the empty-batch guard is unreachable at the 256 KB validation limit.
- [ ] **Step 2: Run** `docker compose up -d`, wait for `http://127.0.0.1:5300/health`, then `dotnet test tests/Ark.Tools.MediatorFramework.Tests --filter "FullyQualifiedName~ServiceBusMessagingBatchSendTests"`. Expected: fails to compile.
- [ ] **Step 3: Implement `SendBatchAsync`.**
  - Map the destination with `ToNativeEntityName` and reuse the `_senders` cache.
  - Run `_validateSize` on every entry first, before any call.
  - Build messages with `_toNativeMessage`, and set `ScheduledEnqueueTime` when `DueTime` has a value.
  - Fill `ServiceBusMessageBatch`es with `TryAddMessage` in order and send each with `SendMessagesAsync` when full and at the end.
  - Time each send and assign its duration to the messages it carried.
  - Throw `InvalidOperationException` (invariant-culture message naming the destination) when a message doesn't fit an empty batch.
  - Dispose every batch.
- [ ] **Step 4: Run** the same filter. Expected: pass. Then run the existing `ServiceBusMessagingTransportConformanceTests` and `MessagingOutboxTests`; expected: pass.
  - If the scheduled test fails because the emulator ignores `ScheduledEnqueueTime` on batch sends, change only the scheduled entries to today's per-message `ScheduleMessageAsync` (as the spec's fallback says), and record that in the PR.
- [ ] **Step 5: CHANGELOG.** Under `### Added`: the new `IMessagingBatchTransport`, for transports that send several outbox messages per broker call. Under `### Changed`: the Service Bus transport now sends each destination's outbox messages of a poll in batches, so a service with a larger `batchSize` makes fewer broker calls; defaults and delivery guarantees are unchanged.
- [ ] **Step 6: Commit** `feat(MediatorFramework): batch service bus outbox sends`, then run a full `dotnet build` with 0 errors and push.
