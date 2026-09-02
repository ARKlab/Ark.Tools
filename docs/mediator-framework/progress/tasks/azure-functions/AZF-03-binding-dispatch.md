# AZF-03 — JSON, route and query binding with scoped dispatch

**Category**: azure-functions · **Priority**: core · **Scope**: FRAMEWORK + GENERATOR

## Problem

A trigger gives the adapter an `HttpRequest`, but it does not reproduce Minimal
API generated envelope binding or SimpleInjector request scope. The helper must
bind the existing contract and invoke its exact decorated handler safely.

## Prerequisites

- AZF-02 merged.
- Review the binding table in
  [`azure-functions-design.md`](../../../azure-functions-design.md) and every
  current Minimal API generator binding test.

## Implementation steps

1. Add one runtime invocation API per handler shape (request, query and command),
   called with generated typed delegates or generic type arguments so no runtime
   handler discovery is needed.
2. Begin and dispose one SimpleInjector `AsyncScopedLifestyle` scope around the
   complete invocation, including binding, handler execution and response writing.
3. Bind route properties from `HttpRequest.RouteValues` using invariant,
   nullable-aware conversion equivalent to ASP.NET Core binding.
4. Bind body-less verb properties and `[HttpQuery]` properties from query
   values, including repeated values/collections supported by Minimal API.
5. For body verbs, deserialize the complete request envelope asynchronously using
   the host-configured ASP.NET Core JSON options. Reject empty/invalid bodies using
   the approved 400 semantics.
6. Reconstruct immutable record envelopes and mutable supported contract shapes
   exactly as the Minimal API generator does. Route/query values overwrite body
   values.
7. Reset every `[ServerSet]` property after all client-controlled input is bound.
   Never deserialize directly into a value that bypasses this final reset.
8. Resolve the exact `IRequestHandler<,>`, `IQueryHandler<,>` or
   `ICommandHandler<>` from the container and execute it with invocation
   cancellation using `async`/`await`.
9. Add host registration that configures `AddMvc().AddJsonOptions(...)` with Ark
   defaults and permits the sample's source-generated `JsonSerializerContext`.
10. Ensure binding failures never invoke a handler and return a typed failure to
    the response layer introduced by AZF-05.

## Caveats

- Do not use synchronous body I/O.
- Do not bind `{version}` into the envelope.
- Do not use `Convert.ChangeType` for NodaTime/custom values unless parity tests
  prove it; reuse existing type converters where available.
- Body size enforcement is a platform/host concern for JSON; attachment-specific
  limits belong to AZF-06.
- Scope disposal must occur after a streamed/download response finishes; coordinate
  ownership with AZF-06 rather than disposing resources early.

## Required test coverage

- GET route + query, POST body, and POST route + query + body combination.
- Route/query overwrite spoofed body values.
- `[ServerSet]` input is reset for JSON and query paths.
- Missing, malformed, overflow and unsupported scalar/collection values return 400
  and do not call the handler.
- Ark JSON behavior covers NodaTime, enum-as-member and polymorphic sample types.
- Decorated handler resolution proves FluentValidation and authorization
  decorators remain in the chain.
- Cancellation reaches the handler and scope disposal runs on success/failure.

## Outcomes

- Generated triggers dispatch the same request envelope into the same SimpleInjector
  graph as Minimal API.
- JSON and scalar binding parity is executable independently from a Functions host.

## Acceptance

- [x] Route/query/body precedence matches the documented Minimal API rules (`AzureFunctionsBoundaryTests.cs:141-170`; generator snapshot tests).
- [x] Server-owned fields cannot be mass-assigned (`ArkAzureFunctionsInvocation.cs:186` resets `[ServerSet]` properties after binding, and the generator excludes them from route/query binding — corrects an earlier claim that Azure Functions has no `[ServerSet]` handling; no dedicated Functions-path test exists for this specific behavior though).
- [x] JSON options match the sample's existing JSON wire format.
- [x] No reflection-based mediator dispatch is introduced.
- [ ] Invocation scope and cancellation behavior are tested (cancellation rethrow is snapshot-tested, `GeneratorSnapshotTests.cs:346-361`; no explicit scope-disposal test exists).
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
