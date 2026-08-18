# AZM-14 — Rebus compatibility and passthrough outbox

**Category**: azure-functions-messaging · **Priority**: compatibility
**Depends on**: AZM-02, AZM-08, AZM-09, AZM-13
**Scope**: FRAMEWORK API + REBUS ADAPTER + SAMPLE COMPOSITION
**Design**: [Sample proof](../../azure-functions-messaging-design.md#12-sample-proof), [Restricted bus shim](../../azure-functions-messaging-design.md#9-restricted-bus-shim)

## Problem

The Book application currently depends directly on Rebus `IBus`, Rebus
`IFailed<T>`, and the durable Rebus outbox. The same application handlers
cannot run on a Mediator Framework network until those APIs are
transport-neutral. Rebus and Mediator Framework persisted messages remain
wire-incompatible and must never share one logical bus.

## Execution map

- **Rebus projects**: update `Ark.Tools.MediatorFramework.Rebus` and
  `Ark.Tools.MediatorFramework.Rebus.Generators`; preserve
  `RebusMessageAttribute` as a supported legacy surface.
- **Application project**: replace `Rebus.Bus.IBus` and
  `Rebus.Retry.Simple.IFailed<T>` dependencies in
  `Ark.MediatorFramework.Sample.Application` with framework abstractions.
- **Rebus hosts**: preserve the existing
  `ApplicationComposition.ConfigureRebusOutbox` calls in
  `Ark.MediatorFramework.Sample.WebInterface/SampleComposition.cs` and
  `Ark.MediatorFramework.Sample.RebusProcessor/RebusProcessorComposition.cs`.
- **Backend selection**: a framework `IBus` backed by the Rebus adapter uses
  the real `Ark.Tools.Outbox.Rebus` implementation. Only the native Mediator
  Framework network bus uses the passthrough outbox.
- **Passthrough semantics**: buffer in the application context, commit durable
  state, then flush once. On flush failure, preserve committed state, throw the
  transport error, and retain diagnostic correlation IDs.
- **Stop condition**: no AMF/Rebus header translation and no attempt to consume
  a persisted message produced by the other stack.

## Implementation steps

1. Move the restricted `IBus` and `IFailed<T>` contracts to a
   transport-neutral Mediator Framework package.
2. Extend Rebus generation/routing to understand the new `[Message]` and
   `[Event]` ownership metadata while preserving legacy `[RebusMessage]`
   behavior. Diagnose conflicting dual declarations.
3. Register a Rebus `IBus` adapter that proxies `Send`, delayed `Send`,
   `Publish`, additional headers, and cancellation to the supported Rebus APIs.
   Rebus composition supplies its host identity to enforce the same
   owner-matched publish rule; an identity-less Rebus sender cannot publish.
4. Map Rebus `IFailed<T>` to the framework `IFailed<T>` so application failure
   handlers contain no Rebus types.
5. Keep Rebus serialization, headers, pipeline, worker, retry, DataBus, and
   outbox configuration independent from every Mediator Framework network.
   Document that the stacks are not wire-interoperable; do not test for the
   absence of interoperability, because it is neither required nor expected.
6. Add a non-durable passthrough outbox for the Mediator Framework sample
   composition. It buffers sends in the application transaction context,
   commits database state first, and then sends directly through the framework
   bus.
7. Surface passthrough send failures after commit. Do not retry silently or
   report transaction rollback after state has committed.
8. Document that the passthrough implementation is a composition compatibility
   seam, not durable outbox support, and can lose dispatch after a successful
   database commit.
9. Preserve the application's enlistment shape without adding outbox methods
   to `IBus`: add a transport-neutral `Enlist(IBus, IOutboxContextCore)`
   extension/transaction scope. The native bus serializes an outgoing AMF
   envelope into the enlisted context instead of sending immediately.
10. In Mediator Framework sample mode, configure the data context with a
    passthrough `IOutboxContextCore`: `SendAsync` buffers envelopes in memory;
    database `CommitAsync` commits durable state first and then flushes each
    buffered envelope through an internal raw-envelope sender. Clear the buffer
    only after each successful send and surface partial-flush diagnostics.
11. In Rebus mode, the framework bus adapter and enlistment scope delegate to
    the existing `Ark.Tools.Outbox.Rebus` transaction/outbox behavior; do not
    reimplement the durable Rebus outbox.
12. Keep `WebInterface` registered as a Rebus one-way sender with
    `ConfigureRebusOutbox(..., startProcessor: false)`. Do not substitute the
    passthrough outbox merely because application handlers now inject the
    framework `IBus`.
13. Keep `RebusProcessor` registered with
    `ConfigureRebusOutbox(..., startProcessor: true)` so it continues to run
    the durable outbox processor. Preserve existing SQL and in-memory outbox
    profiles and their cleanup/processing behavior.
14. Make the native AMF composition and Rebus composition mutually exclusive
    for the outbox registration. Startup must fail if both the passthrough and
    Rebus outbox adapters are registered for one application context.

## Guide contribution

Update [`guide/rebus.md`](../../../guide/rebus.md),
[`guide/azure-functions.md`](../../../guide/azure-functions.md), and
[`guide/host-setup-and-composition.md`](../../../guide/host-setup-and-composition.md)
with the common application APIs, separate topology modes, non-interoperability,
and the durability difference between Rebus outbox and the passthrough outbox.

## Sample extension

Update the Book application handlers to depend only on the framework `IBus`
and `IFailed<T>`. Keep the existing WebInterface and RebusProcessor durable
Rebus outbox registrations as-is behind the Rebus adapter. Register the native
bus plus passthrough outbox only in Mediator Framework mode. In that mode the
WebInterface (Minimal API) becomes a producer-only network participant: it
registers `[MessagingHost]` with `Role = Producer` and composes only the
configured framework `IBus` from the messaging package.
Run the same Book create, background processing, and exhausted-failure
application behavior in separate transport fixtures.

## Required test coverage

- Legacy `[RebusMessage]` routing remains compatible.
- New message/event ownership metadata drives Rebus routing without Azure
  types.
- Conflicting legacy/new routing metadata is diagnosed.
- Rebus adapter preserves supported send, publish, delay, and additional
  headers.
- Rebus and Mediator Framework `IFailed<T>` reach the same application failure
  handler.
- WebInterface keeps the real Rebus outbox with no local outbox processor.
- RebusProcessor keeps the real Rebus outbox with its processor enabled.
- Resolving the framework `IBus` through the Rebus adapter does not select the
  passthrough outbox.
- Conflicting Rebus and passthrough outbox registrations fail startup.
- Passthrough sends occur only after successful database commit.
- Commit succeeds and send fails: committed state remains, the error is
  surfaced, and no success-shaped fallback is returned.
- Partial passthrough flush identifies which message IDs were sent and which
  remain unsent; it does not silently replay sent messages in-process.

## Outcomes

- Book application handlers are transport-neutral.
- Rebus retains its durable outbox and richer feature set.
- Mediator Framework networks remain cheaper Azure Functions-compatible
  alternatives with an explicit non-durable send gap.

## Acceptance

- [ ] Application code contains no Rebus `IBus` or Rebus `IFailed<T>` dependency.
- [ ] Rebus adapters preserve existing behavior and legacy metadata.
- [ ] Existing WebInterface and RebusProcessor Rebus outbox registrations and
  processing behavior remain unchanged.
- [ ] The passthrough outbox dispatches after commit and is documented as
  non-durable and native-AMF-only.
- [ ] Tests prove the two topology modes separately; non-interoperability is
  documented, not tested.
- [ ] The [task board](../README.md) status for AZM-14 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
