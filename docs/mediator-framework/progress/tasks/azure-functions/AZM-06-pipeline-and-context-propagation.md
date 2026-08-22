# AZM-06 — Incoming/outgoing pipeline and context propagation

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-04, AZM-05
**Scope**: RUNTIME + HOSTING
**Design**: [Pipeline and propagation](../../azure-functions-messaging-design.md#10-pipeline-and-propagation)

## Problem

Generated triggers must support cross-cutting transport behavior without
embedding user-context or OpenTelemetry logic in every generated method.
Rebus provides this through `IPipeline` and direction-specific steps.

## Execution map

- **Prior art**: read `src/common/Ark.Tools.Rebus/ApplicationInsightsStep.cs`,
  `UserFlowStep.cs`, and `Ex.cs` before defining the new contracts.
- **Public API/runtime**: put transport-neutral step/context contracts in
  `Ark.Tools.MediatorFramework`; put transport context adapters and built-in
  steps in `Ark.Tools.MediatorFramework.Messaging`.
- **Ordering**: represent stages with framework-owned stable identifiers and
  validate missing anchors, duplicate registrations, and ordering cycles at
  startup.
- **Lifetime**: resolve step instances through SimpleInjector per invocation
  unless explicitly registered singleton; never cache scoped state.
- **Stop condition**: do not copy Rebus interfaces or expose Azure SDK objects
  in public step contracts.

## Implementation steps

1. Define transport-neutral incoming and outgoing step contracts with
   continuation-based async processing.
2. Define named relative positions around deserialize, dispatch, serialize,
   send, and settlement.
3. Provide registration for custom steps on the participant's host binding
   (the assembly-level host attribute, AZM-10/AZM-13), with deterministic
   ordering.
   Participants referencing one network may intentionally use different steps
   because
   implementations can add heavy dependencies and environment-specific
   behavior. Steps are host-local: they live on the host binding, not on the
   shared participant declaration. The network owns only stable stage
   identifiers and contracts.
4. Implement the existing `ark-user-*` propagation behavior as an opt-in
   built-in step.
5. Implement an opt-in OpenTelemetry step that propagates W3C
   `traceparent`, `tracestate`, and `baggage` and creates/continues an
   activity around message processing.
6. Ensure outgoing steps can add headers before serialization and incoming
   steps can restore context before handler resolution.
7. Reject custom attempts to override reserved routing, content, encoding,
   attachment, and identity headers.
8. Ensure exceptions and cancellation pass through the pipeline unchanged.
   AZM-09 owns handler execution and all completion, abandon, and
   dead-letter settlement decisions.
9. Keep the step contracts independent of Azure Service Bus, Storage Queue,
   and Rebus types.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

The transport-neutral step contracts and contexts (public API project,
namespace `Ark.MediatorFramework`). Steps follow the Rebus continuation model;
contexts carry the header dictionary, payload accessors, scope, and
cancellation, and are never cached across invocations:

```csharp
namespace Ark.MediatorFramework;

/// <summary>Continuation-based incoming step, modelled on Rebus IIncomingStep.</summary>
public interface IMessagingIncomingStep
{
    /// <summary>Processes the incoming context and invokes the rest of the pipeline.</summary>
    Task ProcessAsync(MessagingIncomingContext context, Func<Task> next);
}

/// <summary>Continuation-based outgoing step, modelled on Rebus IOutgoingStep.</summary>
public interface IMessagingOutgoingStep
{
    /// <summary>Processes the outgoing context and invokes the rest of the pipeline.</summary>
    Task ProcessAsync(MessagingOutgoingContext context, Func<Task> next);
}

/// <summary>Per-delivery incoming context. One instance per invocation; never shared.</summary>
public sealed class MessagingIncomingContext
{
    /// <summary>Gets the received headers (read-only; reserved amf1-* keys are framework-owned).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets the prepared payload (after DataBus fetch and bounded decompression).</summary>
    public ReadOnlySequence<byte> Payload { get; }

    /// <summary>Gets the service resolver for the current handling scope (the SimpleInjector
    /// AsyncScopedLifestyle scope; SimpleInjector Scope implements IServiceProvider).</summary>
    public IServiceProvider Scope { get; }

    /// <summary>Gets the processing cancellation token.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Gets a per-invocation items bag for step-to-step state.</summary>
    public IDictionary<string, object> Items { get; }
}

/// <summary>Per-send outgoing context. Steps positioned before serialization may add
/// headers; reserved routing/content/encoding/attachment/identity headers are rejected.</summary>
public sealed class MessagingOutgoingContext
{
    /// <summary>Gets the mutable header map; writes to reserved amf1-* keys throw.</summary>
    public IDictionary<string, string> Headers { get; }

    /// <summary>Gets the destination queue or topic resolved from the generated registry.</summary>
    public string Destination { get; }

    /// <summary>Gets the serialized payload; null before the serialize stage runs.</summary>
    public ReadOnlySequence<byte>? Payload { get; }

    /// <summary>Gets the send cancellation token.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Gets a per-invocation items bag for step-to-step state.</summary>
    public IDictionary<string, object> Items { get; }
}
```

The framework-owned stable stage identifiers. Custom steps register relative
to these anchors on the participant's host binding; startup validates missing
anchors, duplicate registrations, and ordering cycles:

```csharp
namespace Ark.MediatorFramework;

/// <summary>Named relative positions around deserialize, dispatch, serialize, send, and
/// settlement (design §10). The network owns only these identifiers and the contracts.</summary>
public enum MessagingPipelineStage
{
    /// <summary>Incoming: headers parsed and payload prepared; contract not yet deserialized.</summary>
    BeforeDeserialize,

    /// <summary>Incoming: typed contract available; handler not yet dispatched
    /// (reads as "before dispatch").</summary>
    AfterDeserialize,

    /// <summary>Incoming: handler completed; settlement not yet applied (AZM-09 owns it).</summary>
    AfterDispatch,

    /// <summary>Outgoing: headers mutable; contract not yet serialized.</summary>
    BeforeSerialize,

    /// <summary>Outgoing: payload bytes final (compressed/claim-checked); transport not
    /// yet called.</summary>
    BeforeSend,

    /// <summary>Incoming: after complete/abandon/dead-letter settlement was applied.</summary>
    AfterSettlement
}
```

The pipeline invoker skeleton — a reverse fold of the ordered steps into one
continuation chain (identical shape for the outgoing direction):

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Composes ordered steps into a continuation chain, Rebus-style.</summary>
public static class MessagingPipelineInvoker
{
    /// <summary>Invokes the incoming pipeline; terminal is the deserialize+dispatch stage.</summary>
    public static Task InvokeIncomingAsync(
        IReadOnlyList<IMessagingIncomingStep> orderedSteps,
        MessagingIncomingContext context,
        Func<Task> terminal)
    {
        var next = terminal;
        for (var i = orderedSteps.Count - 1; i >= 0; i--)
        {
            var step = orderedSteps[i];
            var continuation = next;
            next = () => step.ProcessAsync(context, continuation);
        }

        // Exceptions and cancellation flow through unchanged; settlement is AZM-09's job.
        return next();
    }
}
```

The opt-in built-in user-context incoming step, mirroring the existing Rebus
`ark-user-*` behavior (see `src/common/Ark.Tools.Rebus/UserFlowStep.cs`):

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Restores the sender's principal from ark-user-* headers before handler
/// resolution. Opt-in per participant host binding.</summary>
public sealed class UserContextIncomingStep : IMessagingIncomingStep
{
    public async Task ProcessAsync(MessagingIncomingContext context, Func<Task> next)
    {
        if (context.Headers.TryGetValue("ark-user-id", out var userId))
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "ark-messaging");
            var principal = new ClaimsPrincipal(identity);
            // Publish the principal into the scoped context-provider resolved from
            // context.Scope, mirroring the Rebus UserFlowStep restore behavior. The
            // outgoing counterpart writes ark-user-* headers from the current principal.
            _setScopedPrincipal(context.Scope, principal);
        }

        await next().ConfigureAwait(false);
    }
}
```

The opt-in OpenTelemetry step sketch — W3C `traceparent`/`tracestate`/`baggage`
propagation with an `Activity` around processing:

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Creates/continues an Activity around message processing from W3C headers.</summary>
public sealed class OpenTelemetryIncomingStep : IMessagingIncomingStep
{
    private static readonly ActivitySource _source = new("Ark.MediatorFramework.Messaging");

    public async Task ProcessAsync(MessagingIncomingContext context, Func<Task> next)
    {
        context.Headers.TryGetValue("traceparent", out var traceparent);
        ActivityContext.TryParse(traceparent,
            context.Headers.GetValueOrDefault("tracestate"), out var parent);

        using var activity = _source.StartActivity(
            "amf.message.process", ActivityKind.Consumer, parent);
        // "baggage" header entries are restored into Activity baggage here.
        try
        {
            await next().ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;   // unchanged; AZM-09 owns settlement
        }
    }
}

/// <summary>Writes traceparent/tracestate/baggage headers from Activity.Current.</summary>
public sealed class OpenTelemetryOutgoingStep : IMessagingOutgoingStep
{
    public async Task ProcessAsync(MessagingOutgoingContext context, Func<Task> next)
    {
        if (Activity.Current is { } current)
        {
            context.Headers["traceparent"] = current.Id!;   // W3C format
            if (current.TraceStateString is { } state)
                context.Headers["tracestate"] = state;
        }

        await next().ConfigureAwait(false);
    }
}
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
incoming/outgoing step registration, relative ordering, user-context
propagation, OpenTelemetry propagation, and reserved-header protection.

## Sample extension

Register the opt-in user-context and OpenTelemetry steps on the applicable Book
sample host bindings/composition. Pipeline behavior is proven in
framework
tests over the InMemory transport in this task; end-to-end Book assertions
through dispatch land with AZM-09.

## Required test coverage

- Deterministic relative ordering around every named stage.
- User context outgoing header creation and incoming principal restoration.
- OpenTelemetry parent/context propagation and activity lifecycle.
- Custom step adds an allowed header.
- Reserved-header override is rejected.
- Step failure and cancellation are observable by the caller/test callback and
  do not corrupt pipeline ordering or context. Handler-failure settlement is
  covered by AZM-09.
- Multiple concurrent invocations do not share step state or context.
- Two participants referencing one network may resolve different step sets
  while each
  participant's ordering remains deterministic.

## Outcomes

- User context and OTel propagation are reusable, opt-in transport steps.
- Future transport concerns can be added without changing generated triggers.
- Send and Publish share the same outgoing pipeline.

## Acceptance

- [x] Incoming/outgoing step APIs are public, documented, and transport-neutral.
- [x] Named ordering is deterministic and tested.
- [x] User-context and OpenTelemetry steps are opt-in and tested.
- [x] Additional header injection and reserved-header protection are tested.
- [x] Pipeline failures and cancellation are explicit and reach the dispatcher
  seam unchanged; AZM-09 verifies their settlement behavior.
- [x] The [task board](../README.md) status for AZM-06 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
