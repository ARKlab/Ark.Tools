# Evaluation PoCs — AoT-ready DI container research

Throw-away proof-of-concept projects supporting [docs/aot-di-container-research.md](../docs/aot-di-container-research.md).

**Intentionally isolated** from the repository build: local `Directory.Build.props`, `Directory.Build.targets` and `Directory.Packages.props` stop inheritance of repo-wide settings (analyzers, CPM, lock files). Not referenced by `Ark.Tools.slnx`, not built by CI.

## Projects

| Project | Candidate | What it proves |
|---|---|---|
| `MediInjectio.Evaluation` | Microsoft.Extensions.DependencyInjection + Injectio 6.1 source generator | Open-generic decorators over closed handler registrations, fallback validator, runtime mediator dispatch, scopes, `ValidateOnBuild`. Documents the NativeAOT failure of runtime-closed decorators and the `DynamicDependency` + value-type-instantiation rooting fix a small Ark generator would emit. |
| `PureDi.Evaluation` | Pure.DI 2.4 | Same feature-set fully source-generated: TT-marker open generics with **custom constrained markers** (`TTQuery : IQuery<TT>`), tag-chained open-generic decorators, exact-match override of TT fallback bindings, scoped child compositions, `Func<T>` roots, `Resolve(Type)` mediator dispatch. Compile-time graph verification. |
| `HandlerGen.Generator` + `HandlerGen.Evaluation` | Ark-owned Roslyn incremental generator over MEDI | **Open-generic decoration with zero manual registration** (§8.4 requirement): the generator discovers every `IQueryHandler<,>` implementation at compile time and emits registrations pre-wrapped in *all* `[HandlerDecorator]` decorators, closed fallback validators, and a query→service dispatch map. `NewFeatureHandler` proves a new handler with no registration code anywhere is guaranteed decorated. Plain constructor calls: warning-free on trimmed and NativeAOT. Also proves Roslyn generators cannot chain — the scanner cannot feed Pure.DI's setup DSL. |
| `PureDiWeb.Evaluation` | Pure.DI 2.4 + `Pure.DI.MS` | ASP.NET Core cross-wiring (F10): controller activation from the composition (`Roots<ControllerBase>()` + `AddControllersAsServices()`, CoreCLR-only — MVC cannot run trimmed, container-independent), minimal-API endpoint + health check resolving the decorated pipeline, framework `ILogger<T>` injected into composition services. Self-checks over real HTTP; trimmed minimal-API path passes. |

## Run

```bash
cd evaluations/MediInjectio.Evaluation && dotnet run
cd evaluations/PureDi.Evaluation && dotnet run
cd evaluations/PureDiWeb.Evaluation && dotnet run -- --with-mvc   # MVC assertions are CoreCLR-only
cd evaluations/HandlerGen.Evaluation && dotnet run                # generated code: obj/*/generated/

# NativeAOT proof
dotnet publish -c Release -p:PublishAot=true -o /tmp/aot && /tmp/aot/<binary>

# Trimmed self-contained proof (the actual §8.1 target)
dotnet publish -c Release -p:PublishTrimmed=true -p:TrimMode=full --self-contained -r linux-x64 -o /tmp/trim && /tmp/trim/<binary>

# HandlerGen.Evaluation only: -p:PublishAot / -p:PublishTrimmed are global and would break the
# netstandard2.0 generator project; use the scoped switches instead
dotnet publish -c Release -r linux-x64 -p:AotApp=true -o /tmp/aot
dotnet publish -c Release -r linux-x64 -p:TrimApp=true -o /tmp/trim
```

Each program is an assert-based self-check: it exits non-zero on any regression.
