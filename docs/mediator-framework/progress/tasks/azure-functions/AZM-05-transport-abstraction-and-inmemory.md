# AZM-05 — Transport abstraction and first-class InMemory transport

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-04
**Scope**: RUNTIME + TRANSPORT
**Design**: [Transport abstraction, packaging, and InMemory transport](../../azure-functions-messaging-design.md#5-transport-abstraction-packaging-and-inmemory-transport)

## Problem

The runtime must not depend on Azure SDK types, and the concrete transport is
selected at runtime while the network only declares required capabilities. A
transport contract and a shipped, fully capable InMemory transport are needed
first so every later task lands runnable and testable without Azure
infrastructure.

## Execution map

- **Transport contract**: define it in
  `Ark.Tools.MediatorFramework.Messaging` (internal-facing; not part of
  the application-visible API surface unless needed for custom transports —
  keep it public but clearly documented as an integrator seam).
- **InMemory transport**: implement in the same package as a first-class,
  shipped transport, not a test helper. Model it on the existing Rebus
  `InMemNetwork` concept (`src/common/Ark.Tools.Rebus/` prior art) but honor
  the fixed receive contract below.
- **Conformance suite**: add a reusable transport-contract test suite in
  `Ark.Tools.MediatorFramework.Tests` structured so later transports (Service
  Bus, Storage Queue) re-run the same assertions where capabilities apply.
- **Runnable state**: at task end, envelopes can be sent, scheduled,
  published, received, settled, and dead-lettered through InMemory; the full
  solution builds and tests green.
- **Stop condition**: no Azure SDK reference, no generated triggers, no
  dispatcher, no bus shim, no compression/DataBus integration in this task.

## Implementation steps

1. Define the transport contract consuming the AZM-04 envelope:
   - `MessagingCapabilities Capabilities { get; }`;
   - a hard maximum inline-envelope ceiling in bytes, plus a deterministic
     measurement seam that measures the completed native representation of an
     envelope, including headers and transport encoding. The DataBus decision
     must use this measurement rather than payload bytes alone (AZM-07);
   - `SendAsync(queueName, envelope, ctk)`;
   - `SendAsync(queueName, envelope, scheduledFor, ctk)` valid only when
     `ScheduledSend` is declared;
   - `PublishAsync(topicName, envelope, ctk)` valid only when `PubSub` is
     declared;
   - a receive seam valid only when `Receive` is declared, delivering a locked
     envelope with a native delivery count and accepting exactly one of
     `CompleteAsync`, `AbandonAsync`, or `DeadLetterAsync(reason)` per
     delivery. This PeekLock-style settlement plus delivery-count contract is
     fixed for every receive-capable transport;
   - an optional management seam (ensure queue/topic/subscription) for
     transports that support broker management.
2. Guard every capability-gated member: invoking an undeclared capability
   throws `NotSupportedException` naming the capability.
3. Implement the InMemory transport with `Capabilities = Receive | PubSub |
   ScheduledSend`:
   - named in-memory queues and topics; topic subscriptions forward a copy of
     each published envelope into each subscriber queue;
   - PeekLock semantics: a delivered message is invisible until completed,
     abandoned, or lock-expired; abandon and lock expiry increment the
     delivery count and requeue; dead-letter moves the envelope to a
     per-queue DLQ readable by tests;
   - configurable lock duration and a test-controllable clock (reuse the
     repository NodaTime clock abstractions) so lock expiry and scheduled
     delivery are testable without real waits;
   - scheduled envelopes become visible at their due time;
   - thread-safe under concurrent senders, competing consumers, and
     settlement races;
   - no hard inline-envelope ceiling (`long.MaxValue`-style unbounded): the
     network payload threshold applies alone.
4. Implement a runtime message pump for receive-capable transports: a
   start/stop async loop that takes locked deliveries and invokes a supplied
   callback. The pump is a long-running receive worker hosted only by test or
   custom hosts, never inside an Azure Functions app; AZM-13 rejects InMemory
   receive in Functions composition. AZM-09 plugs the dispatcher into this
   pump; in this task the pump
   is exercised with test callbacks only.
5. Add startup composition: registering a transport validates its
   `Capabilities` against the network `Requires` using the AZM-01 `Validate`
   contract and fails startup listing missing capabilities.
6. Write the conformance suite as capability-conditional test groups: send,
   scheduled send, publish/forwarding, settlement, delivery count, DLQ, lock
   expiry, competing consumers, and cancellation. Run it fully against
   InMemory.
7. Add XML documentation for every public member.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

The transport contract consumed by every later runtime task. Headers travel as
a plain string dictionary strictly separate from the payload (no envelope
object, no `byte[]`); settlement is fixed PeekLock-style with a native
delivery count; capability-gated members throw `NotSupportedException` naming
the missing capability:

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Transport seam used by the messaging runtime; never exposes Azure SDK types.</summary>
public interface IMessagingTransport
{
    /// <summary>Gets the capabilities this transport implementation declares.</summary>
    MessagingCapabilities Capabilities { get; }

    /// <summary>Gets the hard inline-envelope ceiling in bytes; null means no hard
    /// ceiling (InMemory).</summary>
    long? MaximumInlineEnvelopeBytes { get; }

    /// <summary>Measures the completed native representation of an envelope, including
    /// headers and transport encoding. Claim-check decisions use this measurement, never
    /// payload bytes alone.</summary>
    long MeasureNative(IReadOnlyDictionary<string, string> headers, in ReadOnlySequence<byte> payload);

    /// <summary>Sends to a named queue. A non-null dueTime requires ScheduledSend.</summary>
    Task SendAsync(string queue, IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload, DateTimeOffset? dueTime, CancellationToken ctk);

    /// <summary>Publishes to a named topic. Requires PubSub.</summary>
    Task PublishAsync(string topic, IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload, CancellationToken ctk);
}

/// <summary>Receive seam, valid only when Receive is declared.</summary>
public interface IMessagingReceiveTransport : IMessagingTransport
{
    /// <summary>Streams locked deliveries from a queue until cancelled.</summary>
    IAsyncEnumerable<IMessagingLockedDelivery> ReceiveAsync(string queue, CancellationToken ctk);
}

/// <summary>One PeekLock-style locked delivery. Exactly one settlement call per delivery.</summary>
public interface IMessagingLockedDelivery
{
    /// <summary>Gets the received headers.</summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets the received payload bytes.</summary>
    ReadOnlySequence<byte> Payload { get; }

    /// <summary>Gets the native delivery count, starting at 1. Never copied into headers.</summary>
    int DeliveryCount { get; }

    /// <summary>Completes (removes) the delivery.</summary>
    Task CompleteAsync(CancellationToken ctk);

    /// <summary>Abandons the delivery; it becomes visible again and the next lock
    /// acquisition increments the delivery count.</summary>
    Task AbandonAsync(CancellationToken ctk);

    /// <summary>Dead-letters the delivery with a bounded reason and description.</summary>
    Task DeadLetterAsync(string reason, string description, CancellationToken ctk);
}

/// <summary>Optional management seam for brokers that support entity management.</summary>
public interface IMessagingTransportManagement
{
    Task EnsureQueueAsync(string queue, CancellationToken ctk);
    Task EnsureTopicAsync(string topic, CancellationToken ctk);
    Task EnsureSubscriptionAsync(string topic, string subscription, string forwardToQueue,
        CancellationToken ctk);
    Task DeleteSubscriptionAsync(string topic, string subscription, CancellationToken ctk);
}
```

The InMemory transport skeleton: lock-protected named queues, PeekLock with
expiry on the injected NodaTime clock, a due-time heap for scheduled delivery,
topic fan-out into subscriber queues, and a readable `<queue>-poison`-style
DLQ store:

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>First-class, shipped InMemory transport implementing every capability.</summary>
public sealed class InMemoryMessagingTransport : IMessagingReceiveTransport, IMessagingTransportManagement
{
    private readonly object _gate = new();
    private readonly Dictionary<string, InMemoryQueue> _queues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _subscriptions = new(StringComparer.Ordinal);
    private readonly IClock _clock;          // NodaTime; test-controllable, no real waits
    private readonly Duration _lockDuration; // configurable PeekLock duration

    public MessagingCapabilities Capabilities
        => MessagingCapabilities.Receive | MessagingCapabilities.PubSub
         | MessagingCapabilities.ScheduledSend;

    /// <summary>No hard inline-envelope ceiling: the network payload threshold applies alone.</summary>
    public long? MaximumInlineEnvelopeBytes => null;

    public long MeasureNative(IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        var total = payload.Length;
        foreach (var (key, value) in headers)
            total += Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value);
        return total;   // deterministic; InMemory stores the envelope as-is
    }

    public Task SendAsync(string queue, IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload, DateTimeOffset? dueTime, CancellationToken ctk)
    {
        var envelope = InMemoryEnvelope.Snapshot(headers, payload);   // transport-owned copy
        lock (_gate)
        {
            var q = _getOrAdd(queue);
            if (dueTime is { } due)
                q.Scheduled.Enqueue(envelope, Instant.FromDateTimeOffset(due)); // due-time heap
            else
                q.Visible.Enqueue(envelope);
        }
        return Task.CompletedTask;
    }

    public Task PublishAsync(string topic, IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload, CancellationToken ctk)
    {
        lock (_gate)
        {
            foreach (var subscriberQueue in _subscriptions.GetValueOrDefault(topic, []))
                _getOrAdd(subscriberQueue).Visible
                    .Enqueue(InMemoryEnvelope.Snapshot(headers, payload));   // one copy each
        }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<IMessagingLockedDelivery> ReceiveAsync(
        string queue, [EnumeratorCancellation] CancellationToken ctk)
    {
        while (!ctk.IsCancellationRequested)
        {
            InMemoryLockedDelivery? delivery = null;
            lock (_gate)
            {
                var q = _getOrAdd(queue);
                var now = _clock.GetCurrentInstant();
                q.PromoteDueScheduled(now);          // scheduled → visible at due time
                q.ExpireLocks(now);                  // lock expiry behaves as abandon
                if (q.Visible.TryDequeue(out var envelope))
                {
                    envelope.DeliveryCount++;        // increments on every lock acquisition
                    delivery = q.Lock(envelope, lockedUntil: now + _lockDuration);
                }
            }

            if (delivery is not null)
                yield return delivery;
            else
                await Task.Delay(TimeSpan.FromMilliseconds(10), ctk).ConfigureAwait(false);
        }
    }

    // Settlement on InMemoryLockedDelivery (exactly one call wins; races are thread-safe):
    //  - CompleteAsync   → remove from the lock table; the envelope is gone.
    //  - AbandonAsync    → requeue as visible after the configured retry delay on the test
    //                      clock; the next lock acquisition increments DeliveryCount.
    //  - DeadLetterAsync → move to the per-queue DLQ store ("<queue>-poison"-style) with the
    //                      bounded reason/description; readable via GetDeadLetters(queue).

    /// <summary>Test seam: reads the per-queue readable DLQ store.</summary>
    public IReadOnlyList<InMemoryDeadLetter> GetDeadLetters(string queue) { /* ... */ }

    // IMessagingTransportManagement: EnsureQueueAsync/EnsureTopicAsync create named stores
    // idempotently; EnsureSubscriptionAsync records topic → forwardToQueue fan-out;
    // DeleteSubscriptionAsync removes it. Queues and topics are never auto-deleted.
}
```

The runtime message pump skeleton — a long-running receive worker hosted only
by test or custom hosts, never inside an Azure Functions app (AZM-13 rejects
InMemory receive in Functions composition):

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Start/stop receive loop feeding locked deliveries to a supplied callback.</summary>
public sealed class MessagingReceivePump : IAsyncDisposable
{
    private readonly IMessagingReceiveTransport _transport;
    private readonly string _queue;
    private readonly Func<IMessagingLockedDelivery, CancellationToken, Task> _onDelivery;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public MessagingReceivePump(IMessagingReceiveTransport transport, string queue,
        Func<IMessagingLockedDelivery, CancellationToken, Task> onDelivery)
    {
        _transport = transport;
        _queue = queue;
        _onDelivery = onDelivery;
    }

    /// <summary>Starts the long-running receive loop.</summary>
    public Task StartAsync(CancellationToken ctk)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ctk);
        _loop = Task.Run(() => _runAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task _runAsync(CancellationToken ctk)
    {
        await foreach (var delivery in _transport.ReceiveAsync(_queue, ctk).ConfigureAwait(false))
        {
            // The callback owns the header phase, the generated participant binder, and all
            // settlement. AZM-09 supplies the real dispatcher; this task uses test callbacks.
            await _onDelivery(delivery, ctk).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            try
            {
                if (_loop is not null)
                    await _loop.ConfigureAwait(false);   // observe loop faults on shutdown
            }
            catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }
}
```

The shared conformance-suite shape — later transports (Service Bus, Storage
Queue) subclass it so their semantics cannot drift from InMemory:

```csharp
namespace Ark.MediatorFramework.Tests;

/// <summary>Reusable transport-contract conformance suite; capability-conditional groups
/// are skipped when the transport under test does not declare the capability.</summary>
public abstract class MessagingTransportConformanceTests
{
    /// <summary>Creates a fresh transport under test for one test case.</summary>
    protected abstract IMessagingTransport CreateTransport();

    /// <summary>Test-controllable clock driving lock expiry and scheduled delivery.</summary>
    protected FakeClock Clock { get; } = new(Instant.FromUtc(2024, 1, 1, 0, 0));

    // Capability-conditional groups: send, scheduled visibility, publish fan-out,
    // settlement, exact delivery count, readable DLQ, lock expiry, competing consumers,
    // cancellation, capability guards, native-measurement boundaries.

    [Fact]   // test attribute per the tests project's framework
    public async Task Abandon_requeues_and_increments_delivery_count()
    {
        var transport = (IMessagingReceiveTransport)CreateTransport();
        // send → receive → AbandonAsync → advance Clock → receive again → DeliveryCount == 2
    }
}

/// <summary>Runs the full suite against the InMemory transport.</summary>
public sealed class InMemoryTransportConformanceTests : MessagingTransportConformanceTests
{
    protected override IMessagingTransport CreateTransport()
        => new InMemoryMessagingTransport(Clock, lockDuration: Duration.FromMinutes(1));
}
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
transport contract, the InMemory transport as a first-class option for tests
and local development, the runtime pump, and the capability validation
failure mode.

## Sample extension

Add an InMemory transport composition to the Book sample test infrastructure
so later tasks run Book messaging scenarios without Azure resources. No
application handler changes in this task.

## Required test coverage

- Capability guards throw for undeclared operations.
- Send, scheduled visibility, publish fan-out to multiple subscriber queues.
- Complete removes; abandon requeues and increments delivery count; lock
  expiry behaves as abandon; dead-letter lands in the readable DLQ.
- Delivery count is exact across abandon/expiry cycles.
- Competing consumers never observe one lock twice concurrently.
- Scheduled delivery with a controlled clock; no arbitrary sleeps.
- Startup transport-vs-network capability validation success and failure.
- Inline-envelope boundary tests prove that headers and transport encoding are
  included in the claim-check decision.
- Conformance suite passes fully against InMemory.

## Outcomes

- Every later runtime task develops and tests against a real transport.
- InMemory cannot drift from production transports thanks to the shared
  conformance suite.

## Acceptance

- [ ] Transport contract with fixed PeekLock settlement and delivery-count
  semantics is implemented and documented.
- [ ] InMemory transport implements all capabilities and passes the full
  conformance suite.
- [ ] Capability guards and startup validation are tested.
- [ ] The [task board](../README.md) status for AZM-05 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
