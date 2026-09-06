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
| `ReceiveChannels` | 1 | Reserved; the host currently runs a single receive loop |

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

## Settlement and retries

The processor host does not change settlement: successful handling completes,
fail-fast failures dead-letter, and other failures abandon for retry, exactly as
described in [Delivery settlement and
retries](azure-functions.md#delivery-settlement-and-retries).
