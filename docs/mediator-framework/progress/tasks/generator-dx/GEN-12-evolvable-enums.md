# GEN-12 — Evolve enum contracts without breaking strict clients

**Status**: Implemented · **Category**: generator-dx · **Priority**: Post-release

## Problem

Adding a C# enum member can be a breaking wire change because strict clients
may reject values introduced after they were generated. The framework needs an
explicit opt-in representation that allows unknown values to round-trip safely.

## Design

See `docs/mediator-framework/design.md` → *Evolvable enums* for the full
per-transport rules table, and `docs/mediator-framework/guide/serialization.md`
→ *Evolvable enums* for the consumer-facing usage guide.

`Ark.Tools.Core.EvolvableEnum<TEnum>` is a transport-neutral, opt-in wrapper —
a `public readonly partial struct` with exactly one type parameter constrained
to `enum`. Most of the implementation lives outside
`Ark.Tools.MediatorFramework`, in `Ark.Tools.Core` (the value type itself) and
a set of small adapter packages, one per transport that needs custom wiring:

| Package | Contents |
| --- | --- |
| `Ark.Tools.Core` | `EvolvableEnum<TEnum>`, `EvolvableEnumWireFormat`, `EvolvableEnumConversionException` |
| `Ark.Tools.SystemTextJson` | `EvolvableEnumJsonConverterFactory` (default, name) / `EvolvableEnumIntegerJsonConverterFactory` (opt-in, number); wired into `ConfigureArkDefaults()` |
| `Ark.Tools.Core.Dapper` | `EvolvableEnumTypeHandler<TEnum>` + `EvolvableEnumDapper.Register<TEnum>(format)` |
| `Ark.Tools.Core.Protobuf` | `EvolvableEnumSurrogate<TEnum>` + `RuntimeTypeModel.AddEvolvableEnumSurrogate<TEnum>()` |
| `Ark.Tools.Core.MessagePack` | `EvolvableEnumFormatterResolver` + `MessagePackSerializerOptions.WithEvolvableEnumSupport()` |

Key rules (enforced by a static constructor per closed `TEnum`, failing fast —
wrapped in `TypeInitializationException` per standard CLR semantics):

- `TEnum` must **not** be a `[Flags]` enum.
- `TEnum` **must** declare an explicit `NOT_SET = 0` member, so
  `default(EvolvableEnum<TEnum>)` — an omitted non-nullable contract member —
  always decodes to a safe, defined value.
- The exact backing integral type of `TEnum` (width and signedness, `byte`
  through `ulong`) is preserved bit-for-bit via a single `long` storage field.
- Unknown symbolic names and unknown numeric values are retained on
  deserialization (`IsDefined == false`), never rejected.
- Converting a value to a wire form it cannot represent (e.g. an unknown name
  to a number-only transport) throws `EvolvableEnumConversionException`
  explicitly rather than silently corrupting data.

**Registration is not uniformly automatic.** JSON (via the converter factory)
and MessagePack (via a resolver that inspects the runtime `Type` per call)
both resolve support for any closed `EvolvableEnum<TEnum>` with zero setup.
Dapper's `SqlMapper.TypeHandler` and protobuf-net's `RuntimeTypeModel` both
build a static per-type table ahead of time and have no supported mechanism
for auto-applying a handler/surrogate registered against the open generic
type definition to arbitrary closed instantiations (protobuf-net tracks this
as an open feature request, [protobuf-net#802](https://github.com/protobuf-net/protobuf-net/issues/802)) —
so those two adapters require one explicit registration call per wrapped enum
type, exactly like any other custom value type on those libraries.

`ApiSurfaceGenerator` emits explicit `ENUM Type.Member=value` (strict enums)
and `EVOLVABLE-ENUM Type.Member=value` (the `TEnum` wrapped by an
`EvolvableEnum<TEnum>` contract member) lines for every enum reached from a
contract, so member additions/removals/renumbering are caught as snapshot
drift. `GrpcEndpointGenerator` maps `EvolvableEnum<TEnum>` contract members to
`int64` in generated `.proto` text (never a proto `enum`), matching the
protobuf-net surrogate's wire shape exactly — exported proto clients decode
the same bytes with no special handling.

## Outcomes

- Contracts can opt into an evolvable enum representation.
- HTTP JSON, protobuf/gRPC, and generated clients have documented unknown-value
  behavior.
- Existing strict enum contracts retain their current behavior.

## Acceptance

- [x] Define the opt-in contract/API and its serialization semantics.
- [x] Preserve unknown values on supported transports where the wire format allows.
- [x] Generate client types and tests for known and unknown values.
- [x] Document when strict enums remain appropriate.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
