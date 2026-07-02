# Evaluation PoCs — AoT-ready DI container research

Throw-away proof-of-concept projects supporting [docs/aot-di-container-research.md](../docs/aot-di-container-research.md).

**Intentionally isolated** from the repository build: local `Directory.Build.props`, `Directory.Build.targets` and `Directory.Packages.props` stop inheritance of repo-wide settings (analyzers, CPM, lock files). Not referenced by `Ark.Tools.slnx`, not built by CI.

## Projects

| Project | Candidate | What it proves |
|---|---|---|
| `MediInjectio.Evaluation` | Microsoft.Extensions.DependencyInjection + Injectio 6.1 source generator | Open-generic decorators over closed handler registrations, fallback validator, runtime mediator dispatch, scopes, `ValidateOnBuild`. Documents the NativeAOT failure of runtime-closed decorators and the `DynamicDependency` + value-type-instantiation rooting fix a small Ark generator would emit. |
| `PureDi.Evaluation` | Pure.DI 2.4 | Same feature-set fully source-generated: TT-marker open generics with **custom constrained markers** (`TTQuery : IQuery<TT>`), tag-chained open-generic decorators, exact-match override of TT fallback bindings, scoped child compositions, `Func<T>` roots, `Resolve(Type)` mediator dispatch. Compile-time graph verification. |

## Run

```bash
cd evaluations/MediInjectio.Evaluation && dotnet run
cd evaluations/PureDi.Evaluation && dotnet run

# NativeAOT proof
dotnet publish -c Release -p:PublishAot=true -o /tmp/aot && /tmp/aot/<binary>
```

Each program is an assert-based self-check: it exits non-zero on any regression.
