# Messaging processing

A **processor host** consumes a participant queue in a long-running process:
a worker service, a console host, or any ASP.NET Core host. Register it and the
framework fetches, paces, keeps locks alive, and settles deliveries for you.

You do **not** need this chapter when your receiver is an Azure Function: the
generated trigger and the Functions host own that loop. See
[Azure Functions](azure-functions.md).

## Run a processor host

Compose the participant as a receiver; the composition registers
`MessagingProcessorHost` as the receiver `IHostedService`:

```csharp
services.ConfigureArkMessaging(
    BookMessagingNetwork.CreateOptions(),
    BookMessagingNetwork.Registry,
    messaging => messaging.Receiver<PrintingParticipant>(container, receiver => receiver
        .UseTransport(transport => transport.UseServiceBus())
        .UseDataBus(dataBus => dataBus.UseInMemory())));
```

Tune it by registering the options; the host uses its defaults when none are
registered:

```csharp
services.AddSingleton(new MessagingProcessingOptions
{
    MaximumConcurrency = 32
});
```

The defaults are meant to be left alone: the host starts at
`InitialConcurrency` workers (processor count), prefetches twice that, adapts
concurrency to what the workload actually sustains, and renews locks while a
delivery is buffered or in flight. Composition fails at startup — never at 3 a.m.
in production — when the options and the transport cannot be reconciled, for
example when a transport that cannot renew locks is paired with a handler
duration longer than its lock.

Everything else on this page is tuning. Read
[processor-host mechanics](../how-it-works/messaging-processor-host.md) when you
want the algorithms behind these knobs.

## Features and when to use them

### Declare backpressure from a handler

When the handler *knows* its downstream limit — a connection pool size, a
documented rate limit, an HTTP 429 — say so. No metric infers this reliably:

```csharp
if (pool.Exhausted)
    throw new MessagingBackpressureException("SQL pool exhausted", null, TimeSpan.FromSeconds(5));
```

The delivery is abandoned for retry rather than counted as a failure, and the
host halves its concurrency limit immediately.

### Pin the concurrency limit

Set `AdaptiveConcurrency = false` when the correct concurrency is dictated from
outside — a licensed connection count, a partner-imposed rate, a benchmark you
must reproduce. The limit stays at `InitialConcurrency` and no measurement work
is performed. `InitialConcurrency = 1` restores strictly sequential processing.

### Replace the concurrency algorithm

Register your own `IMessagingConcurrencyController` when you have a better
signal than throughput and latency, for example a broker-reported queue depth or
a shared budget across hosts. The built-in AIMD controller is registered with
`TryAddSingleton`, so your registration wins.

### Idle cost

Nothing to configure: a host that cannot accept work issues no broker call at
all, and an idle queue backs off exponentially. On Service Bus the wait happens
server-side, so the first message after an idle period arrives immediately
instead of waiting out a client sleep.

## Tuning by symptom

| Symptom | Change | Why |
| --- | --- | --- |
| Handlers are slow and messages lose their lock | Raise the entity lock duration, or lower `MaximumPrefetch` | A delivery waiting in the buffer holds a lock; only renewable transports extend it |
| A downstream dependency is being overwhelmed | Lower `MaximumConcurrency`, or throw `MessagingBackpressureException` | The limit is an upper bound the controller may not exceed |
| Throughput plateaus below expectations | Raise `MaximumConcurrency` and `PrefetchMultiplier` | Growth stops at the first of these caps to bind |
| Idle queue costs too many broker transactions | Raise `MaxPollInterval` | Bounds the idle poll rate; also the maximum idle latency on transports without server-side wait. Past ~30 s the saving per added second of latency becomes negligible |
| First message after idle arrives too late | Lower `MaxPollInterval` | Caps the sleep between polls |
| A transport outage produces a warning storm | Raise `ErrorCooldown` | Applied after transport errors only, never to empty results |
| Restarts drop work back to the queue | Raise `ShutdownTimeout` | The drain window before unprocessed deliveries are abandoned |
| Concurrency oscillates on a bursty queue | Raise `ConcurrencyEvaluationInterval` | Fewer, better-averaged decisions |

## Settings reference

Everything below lives on `MessagingProcessingOptions`. Options are validated at
composition time; an impossible combination throws `MessagingCompositionException`
with the `ProcessingOptionsInvalid` diagnostic.

### Concurrency

| Setting | Default | What it is for |
| --- | --- | --- |
| `AdaptiveConcurrency` | `true` | Let the limit follow measured throughput and latency. `false` pins it and disables measurement |
| `InitialConcurrency` | processor count | Starting worker count, and the fixed limit when adaptation is off |
| `MinimumConcurrency` | 1 | Floor the controller may never go below |
| `MaximumConcurrency` | 8 × processor count | Ceiling, and the natural place to express a downstream limit |
| `ConcurrencyEvaluationInterval` | 5 s | How often the controller decides |
| `ThroughputImprovementThreshold` | 0.05 | Throughput noise band that growth must beat |
| `GradientIncreaseThreshold` | 0.9 | Latency gradient required to grow |
| `LittlesLawSlack` | 2 | Slack over the useful concurrency implied by throughput × latency |
| `BaselineRearmInterval` | 10 min | How often the no-load latency baseline is re-measured |
| `ThreadPoolStarvationThreshold` | 250 ms | Scheduling delay treated as host starvation |

### Prefetch and buffering

| Setting | Default | What it is for |
| --- | --- | --- |
| `PrefetchMultiplier` | 2 | Buffered deliveries per worker; higher hides broker latency, and holds more locks |
| `MaximumPrefetch` | 8 × `MaximumConcurrency` | Hard ceiling on buffered plus in-flight deliveries |
| `ExpectedHandlerDuration` | 1 s | Per-message estimate used to bound the buffer on transports that cannot renew locks |
| `LockSafetyFactor` | 0.5 | Fraction of the native lock duration a full buffer may take to drain |
| `ShutdownTimeout` | 30 s | Drain window before unprocessed deliveries are abandoned |
| `ReceiveChannels` | 1 | Reserved on the host; Service Bus fan-out is declared on the transport instead |

### Polling and errors

| Setting | Default | What it is for |
| --- | --- | --- |
| `ReceiveWaitTime` | 1 s | Maximum wait asked of the broker per receive |
| `MinPollInterval` | 50 ms | Shortest wait after an empty result |
| `MaxPollInterval` | 30 s | Longest wait after consecutive empty results, and the maximum idle latency |
| `ErrorCooldown` | 10 s | Jittered pause after a transport error |

### Lock renewal

| Setting | Default | What it is for |
| --- | --- | --- |
| `RenewalSafetyMargin` | 10 s | Smallest headroom kept before a lock expires |
| `RenewalScanInterval` | 1 s | How often due locks are scanned |
| `MaximumRenewalBatch` | 64 | Locks renewed per scan, so a large in-flight set cannot stall the timer |

A transport whose lock duration is not longer than `RenewalSafetyMargin +
RenewalScanInterval` fails composition: renewal could never run in time, so the
combination is impossible rather than merely risky.

## Transport receive profiles

The host asks each receive for `min(available credit, MaximumBatchSize)`
deliveries; the rest of the runtime is transport-neutral.

### Azure Storage Queues

| Fact | Value |
| --- | --- |
| `MaximumBatchSize` | 32 (the service maximum for one `ReceiveMessages` call) |
| Server-side wait | No: an empty queue returns immediately and `MinPollInterval`/`MaxPollInterval` own the idle rate |
| Lock renewal | Yes, through `UpdateMessage` |
| Native lock duration | The receive visibility timeout you configure on the transport |

Every receive is one billed transaction whether it returns 1 or 32 messages, so
batching cuts receive transactions by up to 32× at the same throughput. An idle
queue costs roughly one receive every 15 s at the default `MaxPollInterval`
(full jitter averages half the cap).

`UpdateMessage` **rotates the pop receipt**, so renewal and settlement of one
delivery are mutually exclusive and settlement always uses the newest receipt. A
receipt that is no longer valid surfaces as `MessagingLockLostException` rather
than a generic transport error, and the host reports it as a lock-lost signal.

The visibility timeout is the lock: it must cover the handler *plus* the time a
delivery may wait in the prefetch buffer. Use
`StorageQueueMessagingTransport.DeriveReceiveVisibilityTimeout(maximumHandlerDuration,
options)` to compute it instead of guessing; it adds the expected buffer wait and
the renewal safety margin, and throws when the result exceeds the seven-day
service maximum.

Poison handling and dequeue-count semantics are unchanged by batching: one bad
message in a batch of 32 is moved to the poison queue and the other 31 settle
independently.

### Azure Service Bus

| Fact | Value |
| --- | --- |
| `MaximumBatchSize` | Uncapped by default; the service imposes no cap, and an optional hard cap is configurable |
| Server-side wait | Yes: the receive is held open for the host's backoff window |
| Lock renewal | Yes, through `RenewMessageLockAsync` |
| Native lock duration | Declared on the transport (`lockDuration`), or unknown |

Uncapped is the conservative choice here, not the aggressive one. The host never
asks for more than its prefetch budget — the concurrency limit times
`PrefetchMultiplier` — so a batch is proportional to the configured parallelism
and grows only as adaptive concurrency raises the limit. A fixed number such as
100 would be unrelated to what the host can actually drain, and at a low
processing rate it would leave a large batch of locks to renew. Pass
`maximumReceiveBatchSize` only to pin a ceiling *below* the budget, for example
to bound the size of a single AMQP transfer.

`PrefetchCount` stays at 0 on purpose. The host's bounded buffer is the prefetch,
and unlike the AMQP prefetch buffer its locks are visible to the shared renewer.
Settlement stays explicit — no auto-complete, no SDK-owned renewal task — and
concurrency is the host's worker count, so lowering it stops new pickups instead
of cancelling in-flight handlers. `ServiceBusProcessor` is deliberately not used:
its pump receives one message per call on a single link and its internals are
`internal`, so subclassing could not change any of that.

Fan-out is opt-in and ordered: raise `receiveChannels` first (extra receivers,
each its own AMQP link on the same connection), and only then pass extra
`ServiceBusClient` instances via `additionalClients` (extra connections). Service
Bus never hands one message to two links, so fan-out cannot double-deliver.

Failures are mapped, not swallowed: `ServiceBusFailureReason.ServiceBusy` becomes
`MessagingThrottledException` and feeds the controller a broker-throttled signal;
`MessageLockLost` and `SessionLockLost` become `MessagingLockLostException` and
feed a lock-lost signal. Both halve the concurrency limit.

## Provisioning the entity shape

Service Bus entity shape is declared once, next to the transport, and binds from
configuration so a deployment can change it without a rebuild:

```csharp
services.AddMessaging(m => m
    .UseTransport(t => t.UseServiceBus(client, o =>
    {
        o.EnablePartitioning = true;    // create-time only
        o.LockDuration = TimeSpan.FromMinutes(2);
    })));
```

`ServiceBusMessagingOptions` is a plain settable-property class, so
`configuration.GetSection("ServiceBus").Bind(options)` (or
`IOptions<ServiceBusMessagingOptions>`) produces exactly the same result as the
fluent call. On the Azure Functions path, pass the bound instance as the
`serviceBusOptions` argument of `AddArkMessagingFunctionsHost`.

| Setting | Default | Reconcilable? |
| --- | --- | --- |
| `EnablePartitioning` | `false` | **No — create time only** |
| `LockDuration` | 60 s | Yes, updated in place |
| `MaxSizeInMegabytes` | tier default | Yes, where the tier allows |
| `MaxMessageSizeInKilobytes` | tier default | Yes, where the tier allows |

The declared `LockDuration` is also what the transport reports as its native lock
duration, so provisioning and the shared renewer plan against the same number
instead of two numbers that drift apart.

Partitioning is opt-in because it is not free: no cross-partition transactions or
send-batches, `SessionId` becomes the partition key, and ordering and duplicate
detection hold only within a partition. Turn it on for a queue that needs more
throughput than one partition can carry, and leave it off otherwise.

Because Service Bus fixes partitioning when the entity is created, a declaration
that disagrees with an existing queue cannot be applied. The reconciler throws
`MessagingCompositionException` with
`MessagingCompositionDiagnostic.ImmutableEntitySettingMismatch`, naming the queue,
both values and the only remediation: delete and recreate the queue (drain it
first, or accept losing its messages) or change the declaration back. On a Premium
namespace partitioning is a namespace-creation choice and cannot be enabled per
entity at all. Mutable settings, `LockDuration` and the size limits, are updated
in place on the existing entity, and a queue that already matches is left
untouched — no update call at all.

Storage Queues have no equivalent knobs: the visibility timeout is a client-side
receive parameter, covered above.

## Settlement and retries

The processor host does not change settlement: successful handling completes,
fail-fast failures dead-letter, and other failures abandon for retry, exactly as
described in [Delivery settlement and
retries](azure-functions.md#delivery-settlement-and-retries).
