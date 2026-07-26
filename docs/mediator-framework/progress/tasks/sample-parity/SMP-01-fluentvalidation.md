# SMP-01 — FluentValidation via SimpleInjector decorators (G2, decision D2)

**Category**: sample-parity · **Priority**: Release blocker · **Scope**: SAMPLE (+ small framework helper)

## Problem

The mediator sample has **no validation layer**: `CreateGreetingHandler` throws an inline
`ValidationException` (`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingHandlers.cs`).
The ReferenceProject demonstrates the recommended, transport-agnostic pattern with existing
framework decorators from `src/common/Ark.Tools.Solid.FluentValidaton`.

## Decision (D2)

Transport-agnostic **FluentValidation decorator at the handler level** is the canonical validation
layer (covers HTTP, gRPC and Rebus uniformly — including the C5 bus path). Not .NET 10 native
`AddValidation`.

## Reference pattern (copy this)

`samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Application/Host/ApiHost.cs` (~lines 288–312):

- `container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton, c => !c.Handled)` — fallback so unvalidated contracts pass.
- Batch-register concrete `IValidator<T>` implementations from the Application assembly.
- Register decorators: `QueryFluentValidateDecorator<,>`, `RequestFluentValidateDecorator<,>` (and the Command variant once FW-01 lands) around the handler interfaces.
- `NullValidator<T> : AbstractValidator<T>` private helper class (see ApiHost.cs ~line 347).

## Steps

1. In `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/ApplicationComposition.cs`
   add the registrations mirroring the reference pattern above (add the `Ark.Tools.Solid.FluentValidaton`
   project reference to the Application csproj; update lockfiles).
2. Move the inline validation from `CreateGreetingHandler` into a `CreateGreetingValidator : AbstractValidator<CreateGreetingRequest>` in the Application project; handler no longer throws `ValidationException` directly.
3. Optional framework helper (recommended, small): add an extension in the mediator SimpleInjector
   composition surface (where handlers are registered) e.g. `RegisterArkFluentValidation(this Container, params Assembly[])`
   doing steps from the reference pattern in one call — so hosts can't partially wire it. Place next
   to the existing handler-registration helper; if no such framework composition helper exists yet,
   implement sample-local only and note the helper as follow-up in the PR description.
4. Ensure the ProblemDetails mapping renders `ValidationException` as 400 with field errors (works
   with FW-03; if FW-03 not yet merged, verify the sample handler maps it).
5. Tests (Reqnroll): invalid create over HTTP → 400 with validation details; same invalid contract
   over gRPC → `InvalidArgument` with rich validation errors (the Google.Rpc path already exists, see
   TransportParityTests); valid request unaffected.

## Outcomes

- Validation runs identically for all transports via decorators; contracts declare validators; the sample demonstrates the canonical Ark pattern.

## Acceptance

- [x] `NullValidator` conditional fallback + decorators registered; handler-level inline validation removed.
- [x] HTTP 400 + gRPC InvalidArgument tests for the same invalid contract pass.
- [x] Valid-path scenarios unchanged.
- [x] Lockfiles updated; full solution build + tests green.
