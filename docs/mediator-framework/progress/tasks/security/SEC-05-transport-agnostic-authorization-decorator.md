# SEC-05 — Transport-agnostic policy authorization decorator (C5 + G8)

**Category**: security · **Priority**: Release blocker · **Scope**: FRAMEWORK + SAMPLE

## Problem

A contract carrying both `[HttpEndpoint]` and `[RebusMessage]` executes the same handler from the
bus with a **header-supplied principal** and **no policy check** — anyone with queue write access
executes as an arbitrary user. Edge (HTTP) authorization never covers the bus path. The docs promise
a transport-agnostic authorization decorator; the framework ships none.

`Ark.ReferenceProject` already demonstrates the pattern:
`samples/Ark.ReferenceProject/Ark.Reference.Common/Services/Auth/PolicyAuthorizeDecorator/`
(`PolicyAuthorizeOrLogicQueryDecorator.cs`, `...RequestDecorator.cs`, `...CommandDecorator.cs`),
registered in `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Application/Host/ApiHost.cs`.
Framework building blocks exist in `src/common/Ark.Tools.Authorization` and `src/common/Ark.Tools.Solid.Authorization`.

## Decision context

This is the mitigation chosen for C5 (per review): handler-level authorization decorator so
policies run identically for HTTP, gRPC and Rebus dispatch. Promote the ReferenceProject pattern to
a first-class framework story.

## Steps

1. Framework: review `Ark.Tools.Solid.Authorization` decorators; if they already cover
   `IQueryHandler<,>`/`IRequestHandler<,>` (and `ICommandHandler<>` once FW-01 lands), add only the
   missing pieces (e.g. a policy-declaration attribute on contracts, mirroring
   `PolicyAuthorizeOrLogic*` from the ReferenceProject) into `Ark.Tools.Solid.Authorization`.
   Do **not** create a new package.
2. Provide a SimpleInjector registration helper (one call) that registers the authorization
   decorators around all handlers, mirroring how ReferenceProject wires them in `ApiHost.cs`.
3. Sample: register the decorators in
   `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/ApplicationComposition.cs`
   and declare a policy on at least one contract that is dual-exposed (`[HttpEndpoint]` + `[RebusMessage]`).
4. Docs: `design.md` cross-cutting section — document that edge auth is transport-local and the
   decorator is the transport-agnostic enforcement layer; contracts dual-exposed to Rebus **must**
   declare policies or accept that queue writers execute them.
5. Tests (Reqnroll in the sample):
   - HTTP call without the required policy claim → 403.
   - Bus dispatch of the same contract with a principal lacking the policy → handler not executed, message failed/dead-lettered.
   - Positive path for both transports.

## Outcomes

- One declarative policy on the contract enforced identically on HTTP, gRPC and Rebus paths.
- One-line host registration; sample demonstrates it end-to-end.

## Acceptance

- [ ] Policy declared once on a contract is enforced for HTTP (403) and Rebus (handler skipped) — tests prove both.
- [ ] Registration is a single helper call in the composition root.
- [ ] No new package introduced; extensions live in `Ark.Tools.Solid.Authorization`/`Ark.Tools.Authorization`.
- [ ] Docs updated; full solution build + tests green.

**Cross-reference**: depends on SMP-01 registration structure (same decorator wiring section of `ApplicationComposition.cs`); coordinate with FW-01 for `ICommandHandler<>` coverage.
