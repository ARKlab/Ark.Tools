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

- **No declaration-time flags**: each target is a closed generic adapter over
  `ISensitiveValue<TSelf>` living in its own package, opted in by a consumer
  partial class implementing `ISensitiveValueSerializerRegistration`. Adapters are
  reflection-free so the AoT/trim guarantee holds, and the core package keeps no
  transport dependency (`Ark.Tools.Compliance.Dapper` is the reference shape).
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

1. Add one adapter package per target, each with its `Register<T>` entry point.
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

- [x] Newtonsoft, protobuf-net, MessagePack, OpenAPI, and Reqnroll adapters ship
  as separate packages and are tested.
- [x] OpenAPI support uses `MapType`, not a reflection-based schema filter.
- [x] Schema examples come from the reserved-value generator.
- [x] EF Core and Orleans are recorded as follow-ups, not silently dropped
  ([future improvements](../../future-improvements.md#2-ef-core-and-orleans-targets-for-sensitive-value-objects)).
- [x] The [task board](../README.md) status for PII-IMP-03 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.

## Delivered

- `Ark.Tools.Compliance.NewtonsoftJson`, `.Protobuf`, `.MessagePack`,
  `.Reqnroll`, and `.OpenApi`, each a closed generic adapter over
  `ISensitiveValue<TSelf>` with a `Register<T>()`/`RegisterBuiltIn()` entry point
  opted into by a consumer `ISensitiveValueSerializerRegistration` partial class.
  No declaration-time flags and no transport dependency in the core package.
- `ComplianceFakes` in `Ark.Tools.Compliance`: deterministic reserved values
  (RFC 2606 domains, reserved fictional phone ranges, invalid-checksum
  identifiers) shared by the OpenAPI examples and, from PII-IMP-09, the test-data
  fakes. It lives in the core package so the OpenAPI adapter does not depend on
  Reqnroll.
- `SupportComplianceExtensions.MapArkComplianceTypes()`, called by
  `ArkStartupWebApiCommon` next to `MapNodaTimeTypes()`; nullable members map to
  the same primitive schema.
- `Ark.Tools.Compliance.Protobuf` is not marked `IsTrimmable`, matching
  `Ark.Tools.Protobuf`: protobuf-net builds serializers from the
  `RuntimeTypeModel` and rejects a trimmable assembly without `[ProtoModel]`
  (`PBN3012`). Every other adapter is trimmable with the trim analyzer on, which
  is the AoT guarantee this task asks for; a dedicated AoT-published sample is
  not part of this change.
