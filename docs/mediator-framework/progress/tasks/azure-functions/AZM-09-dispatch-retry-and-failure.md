# AZM-09 — Scoped dispatch, settlement, retries, and second-level failure

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-04, AZM-05, AZM-06, AZM-08
**Scope**: RUNTIME
**Design**: [Dispatch and scope semantics](../../azure-functions-messaging-design.md#7-dispatch-and-scope-semantics)

## Problem

Receive processing must reproduce the important Rebus processing semantics
without a Rebus processor: fresh scopes, fail-fast DLQ, delivery-count
exhaustion, and inline second-level dispatch. The dispatcher targets the
AZM-05 transport receive contract, so the identical code runs under the
InMemory pump now and under generated Service Bus triggers in AZM-10.

## Execution map

- **Public API**: define framework `MessagingFailed<T>`, exception DTO, and incoming
  message context in `Ark.Tools.MediatorFramework`.
- **Runtime**: implement manual settlement and scoped dispatch in
  `Ark.Tools.MediatorFramework.Messaging` against the transport receive
  contract (locked delivery + native delivery count + complete/abandon/
  dead-letter). No Azure SDK type appears in the dispatcher.
- **Exact exhaustion rule**: second-level retries are enabled or disabled by
  the participant's retry policy, never inferred from handler registrations.
  When disabled,
  deliveries `1..N` run normal `T` and max delivery is `N`. When enabled,
  deliveries `1..N-1` run normal `T` (fail-fast → immediate DLQ, otherwise
  abandon). Delivery `N` runs inline `MessagingFailed<T>` in a fresh scope, or
  immediate DLQ if no handler is registered. Missing `MessagingFailed<T>` is a
  fail-fast condition. `MessagingFailed` success completes; fail-fast throw
  dead-letters; any other throw abandons. Deliveries `N+1..2N` run normal `T`
  again until the transport max (`2N`) dead-letters. `MessagingFailed` runs once, at
  `N`.
- **Lock discipline**: automatic completion is forbidden; configure bounded
  automatic lock renewal; treat lock loss/completion failure as unsuccessful
  processing.
- **Runnable state**: at task end, Book messages sent via AZM-08 are received,
  dispatched, retried, and failure-handled over the InMemory transport; full
  solution builds and tests green.
- **Stop condition**: never send or persist `MessagingFailed<T>` and never perform
  second-level dispatch for malformed/unsupported envelopes or fail-fast
  exceptions.

## Implementation steps

1. Implement typed envelope-to-contract dispatch with one
   `AsyncScopedLifestyle` scope for normal handling, plugged into the AZM-05
   runtime message pump.
2. Populate message context and cancellation before handler resolution.
3. Complete/ack only after successful handler completion.
4. Translate the existing fail-fast marker/mechanism into direct dead-letter
   settlement on the receiving transport.
5. Use the native delivery count from the locked delivery and the
   participant's retry policy (declared or framework default, AZM-02) for
   retry exhaustion. Never copy or increment it in message
   headers. Configure InMemory max delivery to `2N` when second-level retries
   are enabled and `N` otherwise.
6. Define the public `MessagingFailed<T>` command containing the original
   message, serializable exception info, and a read-only native
   delivery-count snapshot. Do not require failure headers on the live
   envelope; attach bounded details only when dead-lettering if the
   transport can carry them.
7. Dispatch the failure wrapper inline in a fresh SimpleInjector scope from the
   catch path at delivery `N` only. Do not enqueue a separate second-level
   message; no `MessagingFailed<T>` is persisted on the bus.
8. If no `MessagingFailed<T>` handler is registered at delivery `N`, dead-letter
   immediately as fail-fast. If the handler throws fail-fast, dead-letter.
   Otherwise abandon and allow normal `T` on later deliveries through `2N`.
9. Make malformed/unsupported envelopes fail fast and prevent second-level
   dispatch.
10. Apply the same settlement policy to an exception propagated by an AZM-06
    pipeline step as to a handler exception: fail-fast dead-letters and every
    other exception abandons. AZM-06 tests propagation only; this task owns
    the physical settlement assertion.
11. Add structured NLog logging with invariant formatting and bounded metadata.
12. Validate manual settlement, lock renewal, and lock-loss behavior through
    the transport contract.
13. Add decision-lock tests for the selected retry strategy and document the
    alternatives from the design: Ark/Rebus terminal second-level failure,
    explicit deferred second-level handling, and delayed first-level
    rescheduling.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

The public second-level failure wrapper and the serializable exception-info
record (public API project, namespace `Ark.MediatorFramework`):

```csharp
namespace Ark.MediatorFramework;

/// <summary>In-memory second-level failure wrapper. Never sent or persisted on the bus;
/// dispatched inline at delivery N only, in a fresh scope.</summary>
public sealed class MessagingFailed<T> : ICommand<MessagingFailed<T>> where T : class
{
    /// <summary>Gets the original deserialized message.</summary>
    T Message { get; }

    /// <summary>Gets a read-only snapshot of the native delivery count.</summary>
    int DeliveryCount { get; }

    /// <summary>Gets the bounded, human-readable error description.</summary>
    string ErrorDescription { get; }
}

/// <summary>Serializable, bounded snapshot of the exception that failed first-level
/// handling. Used for MessagingFailed diagnostics and bounded dead-letter metadata.</summary>
public sealed record MessagingExceptionInfo(
    string ExceptionType,
    string Message,
    string? StackTrace,
    MessagingExceptionInfo? Inner)
{
    /// <summary>Creates a bounded snapshot from a live exception.</summary>
    public static MessagingExceptionInfo From(Exception exception) { /* ... */ }
}
```

The settlement decision as a pure, testable function encoding the design §7
table (`N` = `MaximumDeliveryCount`). Fail-fast classification reuses
`MessagingFailFastException` (and the existing repository fail-fast marker):

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Classification of the outcome of a handler or pipeline-step invocation.</summary>
public enum MessagingExceptionClassification
{
    /// <summary>Completed successfully.</summary>
    None,

    /// <summary>Threw MessagingFailFastException or the repository fail-fast marker.</summary>
    FailFast,

    /// <summary>Threw any other exception.</summary>
    Other
}

/// <summary>Requested settlement for one locked delivery.</summary>
public enum MessagingSettlementDecision
{
    Complete,
    Abandon,
    DeadLetter,
    RunSecondLevel
}

/// <summary>Pure encoding of the retry table; every branch is unit-testable without a
/// transport.</summary>
public static class MessagingSettlement
{
    /// <summary>Decides settlement from the native delivery count, the participant retry
    /// policy, the exception classification, and the current stage.</summary>
    public static MessagingSettlementDecision Decide(
        int deliveryCount,
        IMessagingRetryPolicy retryPolicy,
        MessagingExceptionClassification classification,
        bool isSecondLevelStage)
    {
        if (classification == MessagingExceptionClassification.None)
            return MessagingSettlementDecision.Complete;

        if (classification == MessagingExceptionClassification.FailFast)
            return MessagingSettlementDecision.DeadLetter;      // any delivery, any stage

        if (isSecondLevelStage)
            return MessagingSettlementDecision.Abandon;         // MessagingFailed threw non-fail-fast

        var n = retryPolicy.MaximumDeliveryCount;
        if (retryPolicy.SecondLevelRetriesEnabled && deliveryCount == n)
            return MessagingSettlementDecision.RunSecondLevel;  // inline MessagingFailed<T>, once, at N

        // Deliveries 1..N-1 and N+1..2N (enabled), or 1..N (disabled): abandon and let the
        // transport/host dead-letter at its configured maximum (2N or N).
        return MessagingSettlementDecision.Abandon;
    }
}
```

The dispatch-loop skeleton plugged into the AZM-05 pump (identical logic later
runs under generated triggers). Header phase → fresh `AsyncScopedLifestyle`
scope → generated binder → settlement mapping; header-phase fail-fast never
enters second-level dispatch:

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Receive dispatcher: manual settlement only; no Azure SDK type appears here.</summary>
public sealed class MessagingDispatcher
{
    private readonly Container _container;
    private readonly IMessagingRetryPolicy _retryPolicy;      // participant policy (AZM-02)
    private readonly MessagingPayloadReceiver _payloadReceiver;   // AZM-07 header phase

    /// <summary>Callback wired into MessagingReceivePump (AZM-05) or a generated trigger.</summary>
    public async Task OnDeliveryAsync(IMessagingLockedDelivery delivery, CancellationToken ctk)
    {
        try
        {
            // Header phase (non-generated): bound and classify headers, prepare the payload
            // (DataBus fetch + bounded decompression), resolve the codec from
            // amf1-content-type into an IMessagingPayloadReader for the typed phase.
            var payloadReader = await _prepareHeaderPhaseAsync(delivery, ctk)
                .ConfigureAwait(false);
            var logicalName = delivery.Headers[MessagingHeaders.MessageType];

            MessagingExceptionInfo? error = null;
            var classification = MessagingExceptionClassification.None;
            try
            {
                // One fresh AsyncScopedLifestyle scope for normal handling.
                await using var scope = AsyncScopedLifestyle.BeginScope(_container);
                // Message context, correlation metadata, and cancellation are populated
                // into the scope before handler resolution.
                var processor = scope.GetInstance<ICommandProcessor>();

                // Typed phase: generated participant binder (AZM-03A) switches over the
                // participant's contract names and calls Deserialize<T> +
                // ICommandProcessor.ExecuteAsync<T> per case. No reflection anywhere.
                await BookParticipantBinder
                    .DispatchAsync(logicalName, payloadReader, processor, ctk)
                    .ConfigureAwait(false);
            }
            catch (MessagingFailFastException ex)
            {
                classification = MessagingExceptionClassification.FailFast;
                error = MessagingExceptionInfo.From(ex);
            }
            catch (Exception ex)   // includes AZM-06 pipeline-step exceptions, unchanged
            {
                classification = MessagingExceptionClassification.Other;
                error = MessagingExceptionInfo.From(ex);
            }

            var decision = MessagingSettlement.Decide(
                delivery.DeliveryCount, _retryPolicy, classification, isSecondLevelStage: false);

            if (decision == MessagingSettlementDecision.RunSecondLevel)
                decision = await _runSecondLevelAsync(
                    delivery, logicalName, payloadReader, error!, ctk).ConfigureAwait(false);

            await _settleAsync(delivery, decision, error, ctk).ConfigureAwait(false);
        }
        catch (MessagingFailFastException ex)
        {
            // Header-phase fail-fast: unknown content type/encoding/contract, foreign
            // network, malformed/oversized headers, oversized payload, attachment
            // integrity. Direct DLQ; never retried, never second-level.
            await delivery.DeadLetterAsync(ex.Reason.ToString(), ex.Message, ctk)
                .ConfigureAwait(false);
        }
    }
}
```

The second-level invocation and settlement mapping. `MessagingFailed<T>` runs in a
FRESH SimpleInjector scope, separate from the normal handling scope; a missing
`MessagingFailed<T>` handler at delivery `N` surfaces as
`MessagingFailFastException(MessagingFailFastReason.MissingSecondLevelHandler)`
and dead-letters immediately:

```csharp
    private async Task<MessagingSettlementDecision> _runSecondLevelAsync(
        IMessagingLockedDelivery delivery, string logicalName,
        IMessagingPayloadReader payloadReader, MessagingExceptionInfo error,
        CancellationToken ctk)
    {
        try
        {
            // FRESH scope, distinct from the normal-handling scope that just failed.
            await using var scope = AsyncScopedLifestyle.BeginScope(_container);
            var processor = scope.GetInstance<ICommandProcessor>();

            // Generated second-level binder: switches on logicalName, deserializes T,
            // wraps it into MessagingFailed<T> with the delivery-count
            // snapshot and error info, and dispatches the participant's
            // ICommandHandler<MessagingFailed<T>> handler through the processor. When no
            // handler is registered for T, it throws
            // MessagingFailFastException(MissingSecondLevelHandler).
            await BookParticipantBinder
                .DispatchFailedAsync(logicalName, payloadReader, delivery.DeliveryCount,
                    error, processor, ctk)
                .ConfigureAwait(false);

            return MessagingSettlementDecision.Complete;        // MessagingFailed success → complete
        }
        catch (MessagingFailFastException)
        {
            // Includes MissingSecondLevelHandler at delivery N → immediate DLQ.
            return MessagingSettlementDecision.DeadLetter;
        }
        catch (Exception)
        {
            // Decide(..., Other, isSecondLevelStage: true) → abandon; normal T resumes on
            // deliveries N+1..2N until the transport maximum dead-letters.
            return MessagingSettlementDecision.Abandon;
        }
    }

    private async Task _settleAsync(IMessagingLockedDelivery delivery,
        MessagingSettlementDecision decision, MessagingExceptionInfo? error,
        CancellationToken ctk)
    {
        switch (decision)
        {
            case MessagingSettlementDecision.Complete:
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
                break;
            case MessagingSettlementDecision.Abandon:
                await delivery.AbandonAsync(ctk).ConfigureAwait(false);
                break;
            case MessagingSettlementDecision.DeadLetter:
                // Bounded reason/description only; never the raw body.
                await delivery.DeadLetterAsync(
                    error?.ExceptionType ?? "fail-fast", error?.Message ?? string.Empty, ctk)
                    .ConfigureAwait(false);
                break;
        }
        // Lock loss or a failed settlement call is surfaced as unsuccessful processing and
        // permits duplicate delivery (at-least-once).
    }
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
scope, settlement, the `N`/`2N` delivery table, fail-fast DLQ, and inline
`MessagingFailed<T>` at delivery `N`. Document that abandon delay is transport-
specific: InMemory/`RetryDelay` and Storage Queue `visibilityTimeout` wait;
Service Bus abandon is immediate.

## Sample extension

Run the Book printing completion, failure, retry-exhaustion, and second-level
scenarios end-to-end over the InMemory transport using the framework bus and
dispatcher. The existing Rebus processor path remains untouched and green.

## Required test coverage

- Successful typed dispatch and completion.
- Fresh scope and cancellation propagation.
- Handler failure followed by retry and eventual success.
- Pipeline-step failure follows the same tested settlement policy as a handler
  failure.
- Delivery `N` with no `MessagingFailed<T>` handler dead-letters immediately.
- Second-level disabled runs normal `T` through `N` and never resolves
  `MessagingFailed<T>`.
- Participant retry policy validation rejects `N = 1` when second-level
  retries are enabled.
- Fail-fast exception goes directly to DLQ at any delivery.
- Inline second-level handler receives original message and serializable error
  info at delivery `N` only.
- Second-level dispatch is inline and uses a separate SimpleInjector scope;
  no failure message is persisted.
- `MessagingFailed` fail-fast throw dead-letters; other throw abandons and the next
  delivery is normal `T`, not `MessagingFailed` again.
- InMemory abandon waits `RetryDelay` on the test clock.
- Lock loss or failed completion is surfaced and permits duplicate delivery.
- Unsupported protocol/type never enters second-level dispatch.
- Native delivery count is unchanged in the message and available in runtime
  context/failure diagnostics only.
- Duplicate delivery remains safe under at-least-once semantics.

## Outcomes

- Receive processing has explicit, tested settlement behavior on a real
  (InMemory) transport before any Azure binding exists.
- Second-level handling uses Rebus concepts without claiming identical retry
  behavior or a Rebus dependency.
- Every failure path is observable and bounded.

## Acceptance

- [x] Normal and second-level handling use separate SimpleInjector scopes.
- [x] Fail-fast and unsupported-read paths go directly to DLQ.
- [x] With second-level enabled, delivery `N` runs `MessagingFailed<T>` or immediate
  DLQ and max delivery is `2N`.
- [x] Participant retry policy, not handler discovery, selects `N` or
  `2N` behavior.
- [x] Native delivery count controls retry exhaustion.
- [x] Failure metadata is serializable, bounded, and tested.
- [x] Structured logging contains no interpolated messages or raw bodies.
- [x] Book scenarios run end-to-end over InMemory.
- [x] The [task board](../README.md) status for AZM-09 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
