# GEN-03 — Startup handler-registration verification (B4)

**Category**: generator-dx · **Priority**: Non-blocking (pre-release if capacity) · **Scope**: FRAMEWORK

## Problem

`design.md` claims handler wiring is "validated at startup", but a contract mapped by
`MapArkEndpoints`/`MapArkGrpcServices`/`RegisterArkRebusHandlers` whose
`IQueryHandler<,>`/`IRequestHandler<,>`/`ICommandHandler<>` is not registered in the container
surfaces as a **500 at first request**.

## Steps

1. Generators already know the exact closed handler interface per contract. Emit, per transport
   registration method, a verification pass: for each mapped contract, resolve (or `GetRegistration`)
   the handler service from the container/service provider; collect misses; throw one aggregated
   `InvalidOperationException` listing `contract → missing handler interface` pairs.
2. SimpleInjector hosts: prefer hooking `Container.Verify()` semantics — the sample uses SimpleInjector
   (`SampleComposition.cs`); ensure the generated registrations participate so `Verify()` catches
   misses. For the MinimalApi path (resolving via `IServiceProvider`), run the check inside
   `MapArkEndpoints` (which runs at startup).
3. Make the check O(registrations), no handler instantiation side effects (use registration lookup,
   not resolution, where the container supports it; SimpleInjector: `GetRegistration(type) is null`).
4. Tests: host with one unregistered handler → startup throws naming the contract and interface;
   fully-registered sample host starts clean.

## Outcomes

- Missing handler registrations fail at startup with an actionable aggregated message, matching the design promise.

## Acceptance

- [x] Unregistered handler → startup failure naming contract + interface (test).
- [x] All three transports verified (HTTP, gRPC, Rebus registration paths).
- [x] No behavioral change for correctly-registered hosts; sample tests green.
- [x] Full solution build + tests green; `design.md` claim now true.
