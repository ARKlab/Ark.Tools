# GEN-05 — Flow `CancellationToken` through Rebus handler wrappers (A10)

**Category**: generator-dx · **Priority**: Non-blocking (pre-release if capacity) · **Scope**: FRAMEWORK

## Problem

The Rebus generator's emitted `IHandleMessages<T>` wrapper invokes the mediator handler with
`CancellationToken.None` (or default) — see the handler invocation in
`src/mediator-framework/Ark.Tools.MediatorFramework.Rebus.Generators/RebusEndpointGenerator.cs`
(around the wrapper emission, ~line 205). Graceful shutdown / bus stop never propagates cancellation
to in-flight handlers.

## Steps

1. Wire cancellation via the **Rebus `MessageContext` in the generated handler wrapper**: the
   emitted `IHandleMessages<T>.Handle` obtains the token from the ambient message context
   (`MessageContext.Current.GetCancellationToken()` — via `ITransactionContext`/incoming-step
   context items; verify the exact accessor against the Rebus version pinned in
   `Directory.Packages.props`; `Ark.Tools.Rebus` in `src/common/Ark.Tools.Rebus` may already have a
   helper) and passes it to `ExecuteAsync(message, cancellationToken)`. Do **not** bump Rebus.
2. Test: behavioral test where a long-running handler observes cancellation when the bus/host stops
   (or, cheaper, a unit test on the emitted wrapper asserting the token passed to the handler is the
   message-context token, using a fake `IMessageContext`).

## Outcomes

- Handlers invoked from Rebus receive a real cancellation token tied to shutdown/message abort.

## Acceptance

- [ ] Emitted wrapper passes a non-default, context-derived token (test).
- [ ] No Rebus version bump; lockfiles unchanged unless strictly needed.
- [ ] Full solution build + tests green.
