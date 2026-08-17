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

`Ark.Tools.Core.EvolvableEnum<TEnum>` is a transport-neutral, opt-in readonly
struct defaulting to an `int` backing type. `EvolvableEnum<TEnum, TBacking>`
selects another exact enum backing type. Most of the implementation lives outside
`Ark.Tools.MediatorFramework`, in `Ark.Tools.Core` (the value type itself) and
a set of small adapter packages, one per transport that needs custom wiring:

| Package | Contents |
| --- | --- |
| `Ark.Tools.Core` | both `EvolvableEnum` forms, type converter, analyzer diagnostics, wire format, conversion exception |
| `Ark.Tools.SystemTextJson` | `EvolvableEnumJsonConverterFactory` (default, name) / `EvolvableEnumIntegerJsonConverterFactory` (opt-in, number); wired into `ConfigureArkDefaults()` |
| `Ark.Tools.Dapper` | type handlers and explicit registration for both wrapper forms |
| `Ark.Tools.Protobuf` | exact-backing surrogates and explicit registration for both wrapper forms |
| `Ark.Tools.MessagePack` | resolver and formatter support for both wrapper forms |

Key rules (enforced by a static constructor per closed `TEnum`, failing fast —
wrapped in `TypeInitializationException` per standard CLR semantics):

- `TEnum` must **not** be a `[Flags]` enum.
- `TEnum` **must** declare an explicit `NOT_SET = 0` member, so
  `default(EvolvableEnum<TEnum>)` — an omitted non-nullable contract member —
  always decodes to a safe, defined value.
- `TBacking` must exactly match the enum backing type and is stored directly;
  the one-argument form selects `int`.
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
drift. `GrpcEndpointGenerator` maps the exact backing category to
`int32`, `uint32`, `int64`, or `uint64` in generated `.proto` text.

## Outcomes

- Contracts can opt into an evolvable enum representation.
- HTTP JSON, protobuf/gRPC, and generated clients have documented unknown-value
  behavior.
- Existing strict enum contracts retain their current behavior.

## Benchmark results

`benchmarks/Ark.Tools.Benchmarks/EvolvableEnumBenchmarks.cs` compares strict and
evolvable enum parsing/formatting and serializes and deserializes arrays of 100
records containing the enum field. Run it with:

```bash
dotnet run --project benchmarks/Ark.Tools.Benchmarks/Ark.Tools.Benchmarks.csproj \
  --configuration Release -- --filter '*EvolvableEnumBenchmarks*'
```

The Release in-process .NET 10 run used BenchmarkDotNet 0.15.8's adaptive
measurement algorithm on the repository's AMD EPYC 7763 CI host. BenchmarkDotNet
recommends leaving warmup and iteration counts automatic: its defaults target at
least 15 measurement iterations and continue until the configured error
threshold is met, instead of fixing a small count. Mean is CPU time per
operation; allocations are managed bytes per operation.

| Operation | Mean | Allocated |
| --- | ---: | ---: |
| `Enum.TryParse` (defined) | 17.192 ns | 0 B |
| `EvolvableEnum.TryParse` (defined) | 4.144 ns | 0 B |
| `Enum.TryParse` (unknown) | 19.046 ns | 0 B |
| `EvolvableEnum.TryParse` (unknown) | 15.996 ns | 0 B |
| `Enum.ToString` (defined) | 10.528 ns | 24 B |
| `Enum.AsString` (defined) | 244.338 ns | 72 B |
| `EvolvableEnum.ToString` (defined) | 4.376 ns | 0 B |
| `Enum.ToString` (undefined) | 20.110 ns | 56 B |
| `Enum.AsString` (undefined) | 92.711 ns | 168 B |
| `EvolvableEnum.ToString` (undefined) | 11.283 ns | 32 B |
| STJ serialize `Enum[]` records | 10,085.571 ns | 6,720 B |
| STJ serialize `EvolvableEnum[]` records | 11,974.237 ns | 9,920 B |
| STJ deserialize `Enum[]` records | 27,344.262 ns | 15,488 B |
| STJ deserialize `EvolvableEnum[]` records | 29,402.476 ns | 22,688 B |

The separate `EvolvableEnumBackingTypeBenchmarks` comparison measures the
default wrapper against `EvolvableEnum<TEnum, int>` directly:

| Operation | `EvolvableEnum<TEnum>` | `EvolvableEnum<TEnum, int>` |
| --- | ---: | ---: |
| Defined `TryParse` | 4.1441 ns, 0 B | 4.1237 ns, 0 B |
| Unknown-name `TryParse` | 16.3715 ns, 0 B | 16.0180 ns, 0 B |
| Defined `ToString` | 0.9912 ns, 0 B | 1.2140 ns, 0 B |
| Unknown-number `ToString` | 11.3273 ns, 32 B | 11.1807 ns, 32 B |

The default form uses composition because C# structs cannot inherit from
another struct, and the forwarding wrapper preserves the shorter public API
while the two-parameter form retains exact backing-type fidelity. On the
measured .NET 10 hot paths, the JIT eliminates the forwarding cost; targeted
`AggressiveInlining` attributes are not needed.

## Acceptance

- [x] Define the opt-in contract/API and its serialization semantics.
- [x] Preserve unknown values on supported transports where the wire format allows.
- [x] Generate client types and tests for known and unknown values.
- [x] Document when strict enums remain appropriate.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
