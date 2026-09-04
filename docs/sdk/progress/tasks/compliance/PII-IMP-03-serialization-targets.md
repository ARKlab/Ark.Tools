# PII-IMP-03 — Serialization targets: Newtonsoft, protobuf-net, MessagePack, OpenAPI, Reqnroll

**Category**: compliance-generator · **Priority**: high
**Depends on**: PII-IMP-02
**Scope**: SOURCE GENERATOR TARGETS + NEW PACKAGES + TESTS
**Design**: [Sensitive value objects](../../../privacy-by-default-prd.md#62-sensitive-value-objects),
[Serialisation](../../../privacy-by-default-prd.md#65-serialisation-and-transport)

## Problem

A value object that cannot cross a process boundary is a value object nobody
adopts; the developer goes back to `string` and the whole model collapses. The
transports Ark actually uses — protobuf-net, MessagePack, Newtonsoft — and the
OpenAPI document that describes them must all handle these types correctly and
without reflection.

## Execution map

- **`SerializationTargets`** flags on `[SensitiveValueObject<T>]` select the
  emitted files; every emitted converter is closed-generic and reflection-free
  so the AoT/trim guarantee holds.
- **Newtonsoft.Json**: `JsonConverter` plus a registration entry.
- **protobuf-net**: surrogate `struct` and `RuntimeTypeModel` registration in the
  shape of `Ark.Tools.Protobuf`'s `EvolvableEnumSurrogate<T>`
  (`Ark.Tools.Compliance.Protobuf`).
- **MessagePack**: `IMessagePackFormatter<T>` and a generated resolver entry in
  the shape of `Ark.Tools.MessagePack`'s `EvolvableEnumFormatter<T>`
  (`Ark.Tools.Compliance.MessagePack`).
- **OpenAPI/Swashbuckle** (`Ark.Tools.Compliance.OpenApi`): a generated
  `MapArkComplianceTypes(this SwaggerGenOptions)` in the shape of
  `SupportNodaTimeExtensions.MapNodaTimeTypes`, emitting `MapType<T>` with the
  primitive `Type`/`Format`, an `x-ark-classification` vendor extension, and an
  example drawn from the RFC 2606 reserved-value generator. It must be a
  `MapType` mapping, never an `ISchemaFilter`: a filter reflects over the type at
  startup and is AoT-hostile. `ArkStartupWebApiCommon` calls the generated
  extension by default.
- **Reqnroll**: value retriever and comparer registration for test projects.
- **Out of scope, tracked as follow-ups**: EF Core value converters and Orleans
  surrogates; they must carry storage policy and grain-state versioning
  semantics and are not bare converters.

## Implementation steps

1. Extend the generator with the target flags and one emitted file per target.
2. Create the `.Protobuf`, `.MessagePack`, and `.OpenApi` packages with the
   minimal dependency each target requires, so no consumer pays for a transport
   it does not use.
3. Wire `MapArkComplianceTypes` into `ArkStartupWebApiCommon` alongside the
   NodaTime mapping.
4. Add the reserved-value example generator shared with PII-IMP-09.
5. Update `Directory.Packages.props` and every `packages.lock.json`.

## Required test coverage

- Round-trip per target: the wire form is cleartext and the rehydrated value
  still renders redacted for `ToString()` and `DebuggerDisplay`.
- The generated OpenAPI document types a classified property as its primitive
  schema, not as an object with a `value` member.
- The schema carries `x-ark-classification` and an RFC 2606 reserved example;
  a test asserts no example matches a real-looking address or number.
- An AoT-published sample serialises every target with no trim warnings.

## Outcomes

- Sensitive value objects are usable on every Ark transport and are documented
  correctly to API consumers.
- The published OpenAPI document doubles as an egress record.

## Acceptance

- [ ] Newtonsoft, protobuf-net, MessagePack, OpenAPI, and Reqnroll targets are
  generated and tested.
- [ ] OpenAPI support uses `MapType`, not a reflection-based schema filter.
- [ ] Schema examples come from the reserved-value generator.
- [ ] EF Core and Orleans are recorded as follow-ups, not silently dropped.
- [ ] The [task board](../README.md) status for PII-IMP-03 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
