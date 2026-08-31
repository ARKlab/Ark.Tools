# AZM-22 — Fluent messaging composition

**Category**: azure-functions-messaging · **Priority**: pre-release
**Depends on**: AZM-18, AZM-21
**Scope**: PUBLIC API + HOSTING + COMPOSITION
**Design**: [Packaging and composition](../../azure-functions-messaging-design.md#packaging), [Host setup](../../../guide/host-setup-and-composition.md)

## Problem

Host developers currently compose messaging through separate transport, codec,
DataBus, participant, bus, lifecycle, pipeline, outbox, and Functions
extensions. The valid combinations and required decisions are difficult to
discover, and callers must invoke generated registration helpers themselves.

Before release, one fluent composition entry point from `IServiceCollection`
must guide every host decision and invoke generated extensions implicitly where
needed, similar to a `ConfigureRebus(...)` setup flow.

## Execution map

- **Canonical entry point**: start one messaging configuration call from
  `IServiceCollection`.
- **Root selection**: fluent configuration selects Functions receiver, custom
  receiver, or producer-only hosting through related root builders.
- **Complete decisions**: guide transport, DataBus, codecs, pipelines, resource
  lifecycle, outbox producer/processor, network, and participant configuration.
- **Generated integration**: resolve generic declarations and invoke generated
  network, participant, and host extensions implicitly.
- **Container boundary**: native-host and infrastructure integration stays in
  `IServiceCollection`; host-independent application handlers and concerns stay
  in SimpleInjector.
- **Replacement**: remove the current low-level public setup extensions after
  migrating internal/generated callers. Do not retain two canonical APIs.
- **Validation**: reject missing, duplicate, incompatible, and host-forbidden
  choices before the service provider starts.

## Implementation steps

1. Add one `IServiceCollection` entry point returning the common messaging root
   configurer.
2. Require the caller to select exactly one hosting mode: Functions receiver,
   custom receiver, or producer-only.
3. Provide related mode-specific builders with common method names and shared
   sub-builders for transport, DataBus, serialization, pipeline, lifecycle, and
   outbox choices.
4. Support the complete decision set in the fluent flow. Defaults are allowed
   only where the existing design already defines one; required infrastructure
   choices remain explicit.
5. Bind one generic network/participant/host declaration per configuration call.
   Additional participants or networks use additional top-level configuration
   calls rather than collection-valued root state.
6. Invoke generated registration and validation extensions internally in the
   correct order. Host developers never call generated setup helpers directly.
7. Keep application handler/decorator registration in SimpleInjector and bridge
   it only where receive dispatch or pipelines require application resolution.
8. Prevent Functions composition from selecting InMemory receive or hosting an
   outbox processor. Preserve producer-only and custom-host capabilities.
9. Preserve mutual exclusion of native/Rebus bus and outbox modes for one
   topology.
10. Make configuration order deterministic and report incomplete/duplicate
    selections with actionable startup errors.
11. Internalize or remove superseded low-level public registration extensions.
12. Migrate all samples and tests, update API baselines, and inspect generated
    composition output.

## Core code shapes

Conceptual shape; final names are selected by this task:

```csharp
services.ConfigureArkMessaging<SampleNetwork>(messaging =>
{
    messaging.Producer<PublisherParticipant>(producer => producer
        .UseAzureServiceBus(configuration)
        .UseAzureBlobDataBus(configuration)
        .UseMessagePack()
        .UseOutbox());
});
```

Functions receiver and custom receiver use sibling mode methods with the same
transport, DataBus, serialization, pipeline, lifecycle, and outbox vocabulary.
Each mode exposes only valid choices. The terminal registration invokes all
generated helpers implicitly.

## Guide contribution

Rewrite host setup, Azure Functions, Rebus, serialization, DataBus, lifecycle,
pipeline, and outbox composition examples around the fluent API. Include a
decision table per hosting mode and clearly separate `IServiceCollection`
infrastructure from SimpleInjector application composition.

## Sample extension

Migrate every Book sender, Functions receiver, custom host, Rebus host, and
outbox processor to the fluent entry point. No sample may call a generated
registration extension directly.

## Required test coverage

- The entry point starts from `IServiceCollection`.
- Every hosting mode exposes the complete valid decision set.
- One call binds one network/participant/host; multiple calls compose
  independently.
- Generated extensions are invoked implicitly and exactly once.
- Missing or duplicate required selections fail before startup.
- Invalid mode choices are unavailable or fail with targeted errors.
- Functions rejects receive InMemory and hosted outbox processing.
- SimpleInjector owns application handlers while infrastructure remains in the
  native service collection.
- Native and Rebus topology modes remain mutually exclusive.
- Superseded low-level public extensions are absent from API snapshots.
- Sample composition resolves and exercises each hosting mode.

## Outcomes

- Host developers follow one discoverable composition flow.
- Generated setup is an implementation detail.
- Native-host and application-container responsibilities are explicit.

## Acceptance

- [x] One fluent `IServiceCollection` entry point configures messaging.
- [x] Related mode builders cover Functions receiver, custom receiver, and
  producer-only hosts.
- [x] All required transport, DataBus, codec, pipeline, lifecycle, and outbox
  decisions are represented.
- [x] Generated extensions are called implicitly.
- [ ] Superseded low-level public setup APIs are removed.
- [ ] Samples, guides, snapshots, and composition tests are migrated.
- [ ] The [task board](../README.md) status for AZM-22 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
