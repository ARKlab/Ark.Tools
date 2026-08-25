# AZM-15 — Three-participant publish/subscribe sample

**Category**: azure-functions-messaging · **Priority**: demonstration
**Depends on**: AZM-08, AZM-09, AZM-10, AZM-12, AZM-13, AZM-14, AZM-14A
**Scope**: SAMPLE + INTEGRATION
**Design**: [Sample proof](../../azure-functions-messaging-design.md#12-sample-proof)

## Problem

The feature must prove that one publisher and multiple subscribers can share a
contract assembly while using different handlers and independent queues.

## Execution map

- **Projects**: extend the existing sample solution with explicitly named
  publisher (publisher-only participant in a non-Functions process) and
  subscriber-A/subscriber-B Functions host projects, each hosting a distinct
  consumer participant, or
  equivalent launchable host compositions; do not create another sample root.
- **Contracts/handlers**: reuse Book application contracts and business
  services. Publisher and subscribers reference the same contract assembly;
  subscriber effects must be observably different.
- **Topology**: publisher identity owns the event topic; each subscriber has
  one identity queue and one forwarding subscription.
- **Two modes**: Book printing scenarios run separately through Rebus and
  Mediator Framework. Tests create messages through the matching sender stack.
  The Rebus publisher-only and consumer participants use
  the same
  network/participant declarations and AZM-14 generated setup.
- **Transport**: automated three-participant tests compose the InMemory
  transport in
  test hosts;
  the Service Bus composition is demonstrated through configuration, generated
  triggers, and optional explicit live/emulator runs.
- **Operations**: provide local settings examples, IaC entity list, startup
  commands, bounded readiness/idle waits, and cleanup commands. Include the
  separate always-running native outbox processor host.

## Implementation steps

1. Add transport-neutral message and event contracts to the sample's
   appropriate contract/application boundary.
2. Add a publisher participant declaring the event in `Publishes`, with no
   event
   handler. Run it in a non-Functions host (for example the Minimal API
   web host or a console sender) composing only the configured `IBus`, to
   prove publisher-only participation outside Azure Functions.
3. Add two subscriber participants, each declaring the event in `Subscribes`
   and each hosted in its own Functions host,
   with
   distinct identity queues, generated
   forwarding subscriptions, and different handlers for the same event.
4. Demonstrate direct queue send, Service Bus publish, scheduled send, context
   propagation, compression, DataBus claim-check, and typed handler binding.
5. Demonstrate retry exhaustion, direct fail-fast DLQ, and inline second-level
   handling with a separate scope.
6. Keep all three messaging participants free of Rebus receive workers and
   outbox processors. Add the AZM-14A custom always-running
   `outbox-processor` host as a separate process.
7. Demonstrate transactional native `Send` and `Publish` enqueue and dispatch
   through that processor without starting it in either Functions subscriber.
8. Add bounded tests proving one topic publication reaches both subscriber
   identity queues and both handlers observe the same contract with distinct
   effects.
9. Document local configuration, IaC expectations, and the Azurite / Azure
   Service Bus emulator (Docker) setup for local runs.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed. Attribute names are the real
`Ark.MediatorFramework` declarations; the declaring classes are `partial`
because AZM-03A generates registry/binder members onto them.

Sample contracts and the three participant declarations sharing one network
(publisher in the Minimal API host; two Functions subscribers with different
handlers):

```csharp
namespace Sample.Contracts;

[Message(Name = "books.print_book")]
public sealed record PrintBook(int BookId) : ICommand<PrintBook>;

[Event(Name = "books.book_print_completed")]
public sealed record BookPrintCompleted(int BookId) : ICommand<BookPrintCompleted>;

/// <summary>Shared retry policy for the subscriber participants.</summary>
public sealed class BookRetryPolicy : IMessagingRetryPolicy
{
    public int MaximumDeliveryCount => 5;
    public bool SecondLevelRetriesEnabled => true;
    public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(5);
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(30);
}

// Publisher-only participant, run by the Minimal API host (no queue, no trigger,
// no handler for its own event). Identity: "web-frontend".
[MessagingParticipant(
    Publishes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class WebFrontendParticipant;

// Subscriber A, hosted in its own Functions app. Identity: "print-notifications".
[MessagingParticipant(
    Subscribes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(BookRetryPolicy))]
public sealed partial class PrintNotificationsParticipant;

// Subscriber B, hosted in a second Functions app with a different handler effect.
// Identity: "print-audit".
[MessagingParticipant(
    Subscribes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(BookRetryPolicy))]
public sealed partial class PrintAuditParticipant;

[MessagingNetwork(
    Members = new[]
    {
        typeof(WebFrontendParticipant),
        typeof(PrintNotificationsParticipant),
        typeof(PrintAuditParticipant)
    },
    Requires = MessagingCapabilities.Receive
        | MessagingCapabilities.PubSub
        | MessagingCapabilities.ScheduledSend)]
public static partial class BookMessagingNetwork;

// Topology derived by generation: topic "web-frontend-book_print_completed" with two
// forwarding subscriptions into the "print-notifications" and "print-audit" queues.
// Each Functions host binds its own participant with
// [assembly: MessagingFunctionsHost(typeof(PrintNotificationsParticipant),
//     MessagingFunctionsTriggerBinding.ServiceBus)].
```

Subscriber handler — plain `Ark.Tools.Solid.ICommandHandler<T>`, reached
through `ICommandProcessor.ExecuteAsync<T>` by the generated binder, and
registered explicitly in each subscriber's composition root:

```csharp
namespace Sample.Application;

/// <summary>Subscriber A effect: records a user-facing notification.</summary>
public sealed class NotifyOnBookPrintCompletedHandler : ICommandHandler<BookPrintCompleted>
{
    private readonly INotificationService _notifications;

    /// <summary>Creates the handler over the notification service.</summary>
    public NotifyOnBookPrintCompletedHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(BookPrintCompleted command, CancellationToken ctk = default)
    {
        await _notifications.NotifyPrintCompletedAsync(command.BookId, ctk).ConfigureAwait(false);
    }
}

// Subscriber A composition root:
//   container.Register<ICommandHandler<BookPrintCompleted>, NotifyOnBookPrintCompletedHandler>();
// Subscriber B registers a different handler for the same contract:
//   container.Register<ICommandHandler<BookPrintCompleted>, AuditBookPrintCompletedHandler>();
```

Reqnroll sketch proving one publish produces two independent deliveries with
distinct handler effects (test hosts compose the InMemory transport;
publisher and subscribers use independent containers sharing only the
transport and intentional scenario state):

```gherkin
Scenario: One published event reaches both subscribers with distinct effects
    Given the book messaging network is composed over the InMemory transport
    When the web frontend publishes a book print completed event for book 42
    And I wait for the messaging network to become idle
    Then the print notifications subscriber recorded one notification for book 42
    And the print audit subscriber recorded one audit entry for book 42
```

```csharp
[Binding]
public sealed class ThreeParticipantSteps
{
    private readonly MessagingScenarioDriver _driver;

    /// <summary>Creates the steps over the scenario-owned messaging driver.</summary>
    public ThreeParticipantSteps(MessagingScenarioDriver driver)
    {
        _driver = driver;
    }

    [When("the web frontend publishes a book print completed event for book {int}")]
    public async Task WhenTheWebFrontendPublishes(int bookId)
    {
        // The driver resolves IBus from the publisher container only.
        await _driver.PublishAsync(new BookPrintCompleted(bookId)).ConfigureAwait(false);
    }

    [When("I wait for the messaging network to become idle")]
    public async Task WhenIWaitForIdle()
    {
        // Bounded polling on the InMemory transport; timeout diagnostics include
        // queue depths, in-flight deliveries, scheduled messages, and DLQ contents.
        await _driver.WaitForIdleAsync().ConfigureAwait(false);
    }

    [Then("the print notifications subscriber recorded one notification for book {int}")]
    public void ThenNotificationRecorded(int bookId)
    {
        _driver.NotificationsSpy.Notified.Should().ContainSingle(x => x == bookId);
    }

    [Then("the print audit subscriber recorded one audit entry for book {int}")]
    public void ThenAuditRecorded(int bookId)
    {
        _driver.AuditSpy.Audited.Should().ContainSingle(x => x == bookId);
    }
}
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
three-participant topology, shared network declaration, independent
subscriptions, and
the choice between Rebus and Azure Functions for Book background activities.

## Sample extension

Extend the existing Book sample, not a parallel demo, with a publisher and two
subscriber Functions hosts. Demonstrate the Book printing/background event
flow through Azure Functions and retain the standalone Rebus processor as an
alternative receiver in a separate non-interoperable topology mode. Use the
framework `IBus` and `IFailed<T>` in application code and Rebus adapters in
Rebus mode. Native Mediator Framework mode uses the SQL outbox and the
dedicated `outbox-processor` custom host from AZM-14A. The existing WebInterface
and RebusProcessor hosts must retain their real Rebus outbox registrations,
with the processor disabled and enabled respectively. Their routing, filtered
dispatch adapters, retry assistance, and event subscriptions come from the
AZM-14 generated Rebus host API. Application handlers remain explicitly
registered by each application composition root.

## Required test coverage

- Publisher has no handler for the published event and runs publisher-only in
  a non-Functions process.
- Subscriber A and B receive one independent copy each.
- Subscriber handlers are different and both run.
- Queue send reaches the processing participant's identity queue.
- Scheduling is observable without arbitrary sleeps.
- Failure and second-level behavior is visible in assertions.
- Hosts can start concurrently without subscription corruption: concurrent
  instances of one participant and of different participants reconcile safely.
- Rebus and Mediator Framework modes run the same application behavior from
  separately produced transport messages.
- Rebus publisher-only/consumer compositions use generated network/participant
  setup;
  consumer subscriptions are awaited after bus start.
- Rebus generation sees contracts only; application handlers remain
  developer-registered and are reached through the processors.
- WebInterface and RebusProcessor still exercise the real Rebus outbox.
- Native SQL outbox enqueue is atomic, and the separate processor preserves
  the original sender identity while dispatching both messages and events.

## Outcomes

- The sample is a concrete operational proof of the ownership and subscription
  model.
- Users can compare the Function message host with the existing Rebus worker.

## Acceptance

- [ ] Three messaging participant projects share the same contract definition.
- [ ] One publish reaches two independently managed subscriber queues.
- [ ] Subscriber handlers are distinct and verified.
- [ ] Sending, scheduling, retry, DLQ, and second-level flows are covered.
- [ ] No Rebus processor or outbox worker runs in any Functions host.
- [ ] The native SQL outbox is drained by the separate `outbox-processor`
  custom host.
- [ ] The [task board](../README.md) status for AZM-15 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
