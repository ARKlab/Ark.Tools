# GEN-05 — Flow `CancellationToken` through Rebus handler wrappers (A10)

**Category**: generator-dx · **Priority**: Non-blocking (pre-release if capacity) · **Scope**: FRAMEWORK

## Problem

The Rebus generator's emitted `IHandleMessages<T>` wrapper invokes the mediator handler with
`CancellationToken.None` (or default) — see the handler invocation in
`src/mediator-framework/Ark.Tools.MediatorFramework.Rebus.Generators/RebusEndpointGenerator.cs`
(around the wrapper emission, ~line 205). Graceful shutdown / bus stop never propagates cancellation
to in-flight handlers.

## Steps

1. Rebus exposes cancellation via the message context: `MessageContext.GetCancellationToken()` /
   `ITransactionContext` items (verify the exact API available in the Rebus version pinned in
   `Directory.Packages.props`; `Ark.Tools.Rebus` in `src/common/Ark.Tools.Rebus` may already have a
   helper). Use it in the emitted wrapper and pass it to
   `ExecuteAsync(message, cancellationToken)`.
2. If the pinned Rebus version lacks a public token accessor, take the token from the hosted-service
   stopping token via an injectable `IHostApplicationLifetime`-based provider — pick the simplest
   approach that compiles against current pins; do **not** bump Rebus.
3. Test: behavioral test where a long-running handler observes cancellation when the bus/host stops
   (or, cheaper, a unit test on the emitted wrapper asserting the token passed to the handler is the
   context token, using a fake `IMessageContext`).

## Outcomes

- Handlers invoked from Rebus receive a real cancellation token tied to shutdown/message abort.

## Acceptance

- [ ] Emitted wrapper passes a non-default, context-derived token (test).
- [ ] No Rebus version bump; lockfiles unchanged unless strictly needed.
- [ ] Full solution build + tests green.
