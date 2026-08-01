# AZF-08 — Add the mediator sample Azure Functions host

**Category**: azure-functions · **Priority**: sample-parity · **Scope**: SAMPLE

## Problem

Framework capability is not complete until a package-shaped Function project hosts
the existing Application contracts and shows realistic composition without
transport logic leaking into handlers.

## Prerequisites

- AZF-01 through AZF-07 merged.
- AZD-01, AZD-03, AZD-04, AZD-06, AZD-08 and AZD-09 decided.
- Use the Azure Functions project scaffolding tool/template approved for the pinned
  SDK, then reduce generated boilerplate. Do not hand-author an approximate
  Functions project.

## Implementation steps

1. Scaffold `Ark.MediatorFramework.Sample.AzureFunctions` under the existing
   sample `src` folder and add it to the solution and package-shaped dependency
   setup.
2. Reference the same Application package and the new Functions transport package.
   Do not reference `WebInterface` or duplicate contracts/handlers.
3. Add the assembly host marker and configure the same external
   `/api/v{version}` surface.
4. Configure `FunctionsApplication.CreateBuilder(args)` and
   `ConfigureFunctionsWebApplication()`, Ark JSON options, authentication,
   authorization services, shared ProblemDetails helpers, SimpleInjector and
   outbound-only Rebus.
5. Add `host.json` with an empty HTTP route prefix; add safe local settings
   templates/placeholders only, with no credentials committed.
6. Factor only truly host-neutral sample composition out of `WebInterface`.
   Keep Kestrel middleware/routing and Functions startup in their respective
   projects.
7. Reuse `ApplicationComposition` so FluentValidation, scope authorization,
   auditing, SQL/in-memory stores, clock and handlers are identical.
8. Ensure every in-scope sample `[HttpEndpoint]` without
   `AcceptsMessagePack = true` is generated. Assert that MessagePack-enabled
   contracts produce the approved diagnostic. Add no handwritten Function methods
   for parity endpoints.
9. Add one shared `[HttpEndpoint(AllowAnonymous = true)]` health contract to the
   Application project so both HTTP hosts expose the same readiness endpoint.
10. Demonstrate versioned read/write, mixed binding, validation, auth, ProblemDetails,
    ETag concurrency, paging, polymorphic JSON, single/multi upload, download,
    approved streaming behavior and an HTTP-to-Rebus send.
11. Update the sample README with local Core Tools startup, required configuration,
    route examples, auth profile and the separate Rebus consumer requirement.

## Caveats

- Azure Functions cannot use ASP.NET Core endpoint middleware or
  `MapArkEndpointsFromAssembly`.
- The Function project is a hosting sibling, not a replacement for WebInterface.
- `samples/` showcases application capabilities and how an application can test its
  composition. Framework-library and Core Tools E2E validation belongs under
  `tests/`, not in the sample projects.
- Do not import MessagePack formatter setup.
- Do not register gRPC services, controllers, Scalar/Swagger middleware or a Rebus
  receive processor.
- Local settings files containing values are ignored; only a scrubbed example may
  be committed.

## Required test coverage

- Build-generated Function metadata contains every in-scope expected route.
- Startup/composition test validates SimpleInjector and required services.
- A guard enumerates Application `[HttpEndpoint]` contracts and fails when the
  sample host lacks a supported generated route.
- No generated Rebus receive handler is registered in the Function container.
- Configuration contains no secrets and missing required values fail clearly.

## Outcomes

- The sample contains two HTTP hosts over one Application assembly: Minimal API and
  Azure Functions isolated worker.
- The Function host visibly demonstrates all approved parity capabilities and
  outbound-only messaging.

## Acceptance

- [ ] Project was scaffolded from an official isolated-worker template.
- [ ] Same Application contracts/handlers are used with no Function annotations.
- [ ] Every supported JSON-only HTTP endpoint is generated with the same external route.
- [ ] Host excludes MessagePack, gRPC and Rebus receive processing.
- [ ] README local-run instructions work from a clean checkout with documented prerequisites.
- [ ] Changed files pass secret scanning.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
