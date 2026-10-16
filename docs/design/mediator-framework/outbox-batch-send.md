# Batch sends in the native messaging outbox

## Status

Accepted ([#1163](https://github.com/ARKlab/Ark.Tools/issues/1163), audit finding IO-03 option B).
Option A (concurrent per-destination sends) is already merged in
[#1138](https://github.com/ARKlab/Ark.Tools/pull/1138).

## Intent

`MessagingOutboxProcessor` sends each outbox message with its own broker call.
Destinations run in parallel since #1138, but the messages of one queue or topic
still go one by one in peek order, each a Service Bus round trip (~5-20 ms) while
the SQL transaction stays open.

No service needs more outbox throughput yet; this came from the audit. The goal
is to have the fast path ready, so the first high-volume service only has to
raise its batch size.

## Goals

- When the transport can send batches, the processor sends each destination's
  messages of a poll with as few broker calls as the transport allows.
- It switches on automatically for a transport that supports it, with no code or
  configuration change in the consuming service.
- Telemetry keeps counting messages: one client-operation metric entry per message.

## Non-goals

- Changing defaults. The outbox batch size stays 10 in `MessagingOutboxProcessor`,
  `AddArkMessagingOutboxProcessor`, `FluentMessagingComposition.UseOutbox` and the
  Functions composition. A service that wants larger polls already passes
  `batchSize` today.
- Poison-message handling (a dead-letter table for messages that keep failing).
  Tracked separately.
- Batch sends for Storage Queues or in-memory. Storage Queues have no batch send
  API; in-memory keeps the single-send path.
- Exactly-once delivery. Delivery stays at-least-once.

## Anti-goals

- A change in what reaches the receiver: the same messages, headers, bodies and
  scheduled times as today.
- Weaker failure handling than today: a failed send must still roll back the
  whole poll.
- A dashboard that counts batches where it counted messages.

## How it works

### Optional transport capability

A new public interface in `Ark.Tools.MediatorFramework.Messaging` that a transport
may implement next to `IMessagingTransport`. The working name is
`IMessagingBatchTransport`; it takes one destination (queue or topic) and the
ordered list of that destination's envelopes from one poll, each with headers,
payload and optional due time, and completes when all are accepted.

The processor checks for the interface once. When the transport implements it,
each destination group goes through it; otherwise the processor keeps today's
per-message `SendAsync`/`PublishAsync` loop. A transport that wraps another one
and does not implement the interface falls back to single sends, which is safe.

### Processor

`ProcessMessagesAsync` keeps everything it does today before sending: header
validation of the whole poll before any send, grouping by destination, and the
parallel loop across destinations with first-failure cancellation. Only the
per-destination body changes: one batch call instead of a loop of single sends.

### Service Bus

`ServiceBusMessagingTransport` implements the interface:

- All of a destination's messages are packed into `ServiceBusMessageBatch`es in
  peek order (`CreateMessageBatchAsync`, `TryAddMessage`) and sent with
  `SendMessagesAsync`. When a batch is full, it is sent and a new one starts, so
  one poll may need several batches per destination. The batch enforces the
  entity's size limit, so no size budget is needed in the transport.
- A scheduled message (queues only; topics already reject a due time) goes in the
  same batch with `ServiceBusMessage.ScheduledEnqueueTime` set to its due time,
  instead of today's `ScheduleMessageAsync`. The receiver sees the same scheduled
  time, the order within a queue stays the peek order, and nothing uses the
  sequence number `ScheduleMessageAsync` returns (today's code discards it).
  *Designer's call*, made after the builder check. The builder confirms the
  Service Bus emulator honours `ScheduledEnqueueTime` on a batch send. If it
  doesn't, scheduled messages fall back to today's single `ScheduleMessageAsync`
  per message rather than one unbounded `ScheduleMessagesAsync` call, which the
  service would reject above its batch size limit.
- Each message keeps today's size validation (`_validateSize`) and today's native
  conversion (`_toNativeMessage`), so the wire format is unchanged.
- A message that does not fit an empty batch fails the poll with a clear
  `InvalidOperationException`, the same outcome as today's single send failing.

### Failure

A batch send is all-or-nothing per call. Any failure fails the poll, nothing is
committed, and the next poll re-sends every peeked message, as today. Earlier
batches of the same poll may already be accepted, so they can be delivered twice;
today's single sends have the same property, so at-least-once is unchanged.

### Telemetry

`MessagingMetrics.RecordClientOperation` is still called once per message, with
the message's own headers, operation (`send`, `defer`, `publish`) and destination.
The duration recorded for each message is the duration of the broker call that
carried it.

## Constraints

- Public API: one new interface. No new parameters and no changed defaults.
- AOT and trimming: same rules as the rest of the messaging package (no
  reflection).
- XML documentation on every public member; `CHANGELOG.md` entries under
  `[Unreleased]`: Added (the interface) and Changed (Service Bus outbox sends are
  batched).
- Tests follow the repository strategy: integration tests against the Service Bus
  emulator from `docker-compose.yml` for the Service Bus path, plus processor
  tests with a fake batch transport for the dispatch logic and the fallback.

## Decided

- Batching switches on automatically when the transport supports it.
- Default batch size stays 10.
- Dead-lettering is out of scope.
- One metric entry per message.

## Left to the builder

- The interface's name and exact signature.
- Whether a benchmark is added to `benchmarks/Ark.Tools.Benchmarks`.
