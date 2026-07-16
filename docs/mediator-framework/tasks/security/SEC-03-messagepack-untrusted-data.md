# SEC-03 — MessagePack `UntrustedData` + startup resolver check (C3 + A3)

**Category**: security · **Priority**: Release blocker · **Scope**: FRAMEWORK

## Problem

`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkMessagePackEx.cs`:

1. Deserializes untrusted HTTP request bodies **without** `MessagePackSecurity.UntrustedData` — exposes hash-flooding and deep-nesting DoS.
2. The design promises a startup-time check that every `[HttpEndpoint(AcceptsMessagePack = true)]` contract has a MessagePack resolver/formatter; today a missing formatter surfaces as a 500 at first request.

## Steps

1. In `ArkMessagePackEx` options construction (`GetOptions` or equivalent), apply `.WithSecurity(MessagePackSecurity.UntrustedData)` to the `MessagePackSerializerOptions` used for **deserialization** of request bodies. Keep serialization options unchanged if separate.
2. Add a startup validation helper, e.g. `public static void ValidateMessagePackContracts(this IServiceProvider/IEndpointRouteBuilder ...)` (pick the shape that fits the existing `MapArkEndpoints` flow — best: run inside the generated `MapArkEndpoints` for each `AcceptsMessagePack` contract):
   - For each MessagePack-enabled contract type, call `options.Resolver.GetFormatterWithVerify<T>()` (via a small generic helper) inside a try/catch and throw a single aggregated `InvalidOperationException` listing all missing formatters with the contract type names.
   - Generator change: emitted `MapArkEndpoints` collects the MessagePack contract types and invokes the validation helper once.
3. Tests:
   - A malicious-payload test is not required; assert the options instance has `Security == MessagePackSecurity.UntrustedData` via a unit test.
   - Startup check: a test host with a MessagePack endpoint whose contract lacks a formatter must fail at `MapArkEndpoints` time with a message naming the type.
4. Update `design.md` §MessagePack to describe the startup check as implemented.

## Outcomes

- MessagePack request deserialization is hardened against untrusted input.
- Missing resolver/formatter is a startup failure with an actionable message, not a runtime 500.

## Acceptance

- [ ] Deserialization options use `MessagePackSecurity.UntrustedData` (unit test asserts it).
- [ ] Host with unformattable MessagePack contract fails fast at startup, error names the contract type (test).
- [ ] Existing MessagePack negotiation sample/tests still pass.
- [ ] Full solution build + tests green.
