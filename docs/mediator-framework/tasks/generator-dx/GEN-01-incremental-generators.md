# GEN-01 — Make generators truly incremental (A1)

**Category**: generator-dx · **Priority**: Non-blocking (pre-release if capacity) · **Scope**: FRAMEWORK

## Problem

All three generators pipe `context.CompilationProvider` through `SelectMany` and walk **every type
in every referenced assembly** on each compilation — defeating incremental generation; IDE re-runs
the full walk on every keystroke.

- `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`
- `src/mediator-framework/Ark.Tools.MediatorFramework.Grpc.Generators/GrpcEndpointGenerator.cs`
- `src/mediator-framework/Ark.Tools.MediatorFramework.Rebus.Generators/RebusEndpointGenerator.cs`

`design.md` specifies a `ForAttributeWithMetadataName` syntax-provider pipeline.

## Steps

1. For **source-declared** contracts: switch to `context.SyntaxProvider.ForAttributeWithMetadataName("Ark.MediatorFramework.HttpEndpointAttribute", ...)` (and the gRPC/Rebus attribute names) producing equatable model records (no `ISymbol` retained in the pipeline — extract strings/primitives into `record` models so caching works).
2. For contracts in **referenced assemblies** (the current reason for the full walk): keep a `CompilationProvider`-based scan but restrict it to `MetadataReference`s whose assembly references `Ark.Tools.MediatorFramework` (cheap pre-filter via `Compilation.GetUsedAssemblyReferences`/assembly-identity check), and combine with the syntax branch. If cross-assembly discovery is not actually required (check the sample: contracts live in `Ark.MediatorFramework.Sample.Application`, referenced by WebInterface — so it IS required), document the cost.
3. Ensure all pipeline stages carry equatable models (`record` with value-type/string members, `EquatableArray` pattern) so unchanged inputs skip regeneration.
4. Add incrementality tests using `GeneratorDriver` (`IncrementalGeneratorRunReasons` — assert cached steps on a no-op recompile). Follow existing generator test layout under `tests/` if present.
5. Verify emitted output is byte-identical to before (snapshot comparison) — this task must not change generated code.

## Outcomes

- Keystroke-level IDE performance: unchanged inputs produce fully cached generator runs; emitted code unchanged.

## Acceptance

- [ ] No `ISymbol`/`Compilation` captured in cached pipeline stages (models are equatable records).
- [ ] `GeneratorDriver` test proves cached/NotRun step reasons on unchanged recompilation.
- [ ] Generated output identical before/after (snapshot test or diff of `.g.cs` in the sample obj).
- [ ] Full solution build + tests green.
