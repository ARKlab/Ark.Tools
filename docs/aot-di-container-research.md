# Research: AoT-ready DI container to replace SimpleInjector

Status: **clarifications received — recommendation issued** (see [§8](#8-clarifications-received-2026-07-02) for the answers and [§7](#7-recommended-solution) for the resulting decision). One question (Q3, migration tolerance) is still open — expanded with examples in §8.3.
Last updated: 2026-07-02.

---

## 1. Context and goal

Ark.Tools relies heavily on SimpleInjector: decoration (incl. open-generic decoration), startup
verification (`Container.Verify()`) and advanced diagnostics, following a strict "no magic /
no ambiguity" philosophy (same rationale as the NodaTime choice).

SimpleInjector is **not** NativeAOT/trimming ready and its maintainer has explicitly refused to
support it (see §3). Goal: find a source-generation-based, AoT-ready DI container (or approach)
that preserves the SimpleInjector feature-set Ark.Tools and its samples rely upon.

---

## 2. Research progress (resumable)

| Step | Status | Where |
|---|---|---|
| Catalog SimpleInjector features used by `src/` and `samples/` | ✅ done | §4 |
| SimpleInjector project AoT status (issues / WIP / forks) | ✅ done | §3 |
| Survey of source-generated AoT-ready DI containers | ✅ done | §5 |
| Feasibility PoC: MEDI + Injectio | ✅ done, passing on CoreCLR + NativeAOT | `evaluations/MediInjectio.Evaluation` |
| Feasibility PoC: Pure.DI | ✅ done, passing on CoreCLR + NativeAOT | `evaluations/PureDi.Evaluation` |
| Trimmed self-contained deployment proof (`PublishTrimmed=true`, `TrimMode=full`) + startup timing | ✅ done, both PoCs pass | §6.6 |
| Clarifications on §8 questions | ✅ received 2026-07-02 (Q3 still open, expanded in §8.3) | §8 |
| Decision on recommended approach | ✅ **Pure.DI primary, MEDI-native fallback** | §7 |
| ASP.NET Core cross-wiring PoC on Pure.DI (`Pure.DI.MS`): controller activation, minimal APIs, health checks | ✅ done — cross-wiring works; **MVC itself cannot run trimmed** (§6.7) | `evaluations/PureDiWeb.Evaluation` |
| Rebus `IHandlerActivator` PoC on Pure.DI | ⬜ not started | — |
| Ark source-generator prototype for handler scanning/binding emission (per Q6) | ⬜ not started | — |
| Migration plan for `Ark.Tools.Solid.SimpleInjector` / `Ark.Tools.SimpleInjector` / `Ark.Tools.AspNetCore` | ⏸ blocked on Q3 answer | §8.3 |

Environment notes for whoever resumes:
* PoCs run with SDK 10.0.301 (repo `global.json`); AOT publish verified on `linux-x64` with `PublishAot=true`.
* `evaluations/` is isolated from repo MSBuild inheritance (own `Directory.Build.props/targets/Packages.props`); it is not in `Ark.Tools.slnx` and produces no `packages.lock.json` churn.
* Package versions evaluated: Pure.DI 2.4.3, Injectio 6.1.0, MEDI 10.0.0, Jab 0.12.0 (not PoC'd, disqualified), StrongInject 1.4.4 (not PoC'd, unmaintained). No known vulnerabilities (GitHub advisory DB checked).

---

## 3. SimpleInjector project: AoT status, WIP, forks

Verified against github.com/simpleinjector/SimpleInjector (mid-2026):

* **Maintainer explicitly refuses AoT support.**
  * [#1013 ".NET Native support not works"](https://github.com/simpleinjector/SimpleInjector/issues/1013) (closed 2024-12-18): *"Simple Injector does not support .NET Native. But it will not support .NET Native in the future… .NET Native applications are better off using Pure DI."* — dotnetjunkie.
  * [#264](https://github.com/simpleinjector/SimpleInjector/issues/264) (closed 2019, wontfix): same position.
  * [#914 "Compile-time Container Creation"](https://github.com/simpleinjector/SimpleInjector/issues/914) (closed 2021): source generators dismissed — *"compile-time DI systems have no advantage over using Pure DI"*.
* **Trimming**: [#972 "Please support for Trim on self-contained apps"](https://github.com/simpleinjector/SimpleInjector/issues/972) is open since 2023, parked in the perpetual `v6.0` milestone. No PRs about AoT/trimming exist.
* **Failure mode on NativeAOT** is not expression compilation (the runtime falls back to the expression interpreter) but **trimmed reflection metadata**: registered types lose ctor metadata; even `SimpleInjector.VerificationOption[]` loses metadata making `Verify()` throw `NotSupportedException` (#1013). No documented workaround; docs repo has zero AOT/trimming content.
* **Project health**: actively maintained (v5.6.0, 2026-06-28) but effectively single-maintainer (dotnetjunkie). v5.6 turned `EnableDynamicAssemblyCompilation` into a compile error (removing the worst AoT blocker), but the default path is still reflection + `Expression.Compile()`.
* **Forks**: none adding AoT support were found.
* **Forking feasibility**: MIT license, core ≈200–250 C# files / ~30–45 kLOC. The AoT problem is architectural (reflection + runtime `Expression` building throughout `Registration.cs`, `Decorators/*`, `CompilationHelpers.cs`); a fork would need pervasive `DynamicallyAccessedMembers` annotation **plus** a source-generation layer to root/monomorphize closed generics. This is a rewrite of the engine, not a patch — see §5.6.

---

## 4. DI features Ark.Tools and the samples rely upon

Catalogued from `src/` and `samples/` (notably `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Application/Host/ApiHost.cs`, `src/aspnetcore/Ark.Tools.AspNetCore/Startup/ArkStartupWebApiCommon.cs`, `src/common/Ark.Tools.SimpleInjector/Ex.cs`):

| # | Feature | Usage sites (representative) | Criticality |
|---|---|---|---|
| F1 | **Open-generic decorators** — `RegisterDecorator(typeof(IQueryHandler<,>), typeof(PolicyAuthorizeQueryDecorator<,>))`; same for `ICommandHandler<>`, `IRequestHandler<,>`, `IHandleMessages<>` (RebusScope/RebusLog), `IValidator<>` samples | `Ark.Tools.Solid.Authorization/Ex.cs`, `ApiHost.cs`, samples' `ApiHost.cs` | **Critical** — the whole Solid pipeline is built on this |
| F2 | Closed decorators — `RegisterDecorator<IDbConnectionManager, SqlConnectionManagerLeakDecorator>()`, `IAsyncDocumentSession` auditing | `ApiHost.cs`, `Ark.Tools.RavenDb.Auditing/Ex.cs` | High |
| F3 | **Conditional/fallback registration** — `RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), c => !c.Handled)`, `PassThroughAuthorizationResourceHandler<,>` | samples' `ApiHost.cs`, `Ark.Tools.Solid.Authorization/Ex.cs` | **Critical** |
| F4 | **Runtime mediator dispatch** — `GetInstance(typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult)))` + `dynamic` invoke | `Ark.Tools.Solid.SimpleInjector/SimpleInjector{Query,Command,Request}Processor.cs` | **Critical** (note: `dynamic` is itself AoT-hostile, must be replaced regardless of container) |
| F5 | Batch registration / assembly scanning — `GetTypesToRegister(typeof(IValidator<>), assemblies)`, `Collection.Register(typeof(IHandleMessages<>), handlers)` | samples' `ApiHost.cs` | High |
| F6 | Collection resolution — `GetAllInstances<IHandleMessages<TMessage>>()` (Rebus activator) | `Ark.Tools.Rebus/SimpleInjectorHandlerActivator.cs` | High |
| F7 | **Startup verification + diagnostics** — `Container.Verify()`, `SuppressDiagnosticWarning`, captive-dependency/lifestyle-mismatch analysis | `WorkerHost.cs`, health checks, everywhere implicitly | **Critical** (philosophy: fail at startup, no runtime surprises) |
| F8 | `AsyncScopedLifestyle` + explicit `BeginScope` (Rebus message scope, WorkerHost per-resource scope) | `RebusScopeDecorator.cs`, `WorkerHost.cs` | High |
| F9 | `Func<T>`/`Lazy<T>` auto factories, variant collections/types — `AllowResolvingFuncFactories()`, `AllowToResolveVariantCollections()` (unregistered-type-resolution events) | `Ark.Tools.SimpleInjector/Ex.cs` | Medium |
| F10 | **ASP.NET Core cross-wiring** — `services.AddSimpleInjector(...).AddAspNetCore().AddControllerActivation()`, `app.UseSimpleInjector()`, health-check adapters resolving from the container | `ArkStartupWebApiCommon.cs`, `Ark.Tools.AspNetCore.HealthChecks` | **Critical** for web apps |
| F11 | Low-level registration API — `Lifestyle.CreateRegistration`, `InstanceProducer`, `ContainerLocking` event, metadata (`Get/SetMetadataValue`) | `Ex.cs`, `WorkerHost.cs`, Rebus/Activity extensions | Medium (internal plumbing, replaceable) |
| F12 | Conditional-on-consumer registration (`RegisterConditional` by consumer type), `RegisterInstance`, singleton factories | various | Medium |

---

## 5. Alternatives evaluated

### 5.1 Pure.DI 2.4.3 (DevTeam/Pure.DI) — MIT, very active (Jun 2026), ~810★

* Fluent compile-time DSL in a partial class; generator emits pure constructor calls — zero reflection, **shipped NativeAOT samples**.
* Open generics via **marker types** (`TT`, `TT1`…), monomorphized at compile time. Constraints expressible with **custom markers** (`[GenericTypeArgument] interface TTQuery : IQuery<TT>` — proven in our PoC).
* Decorators via tag-chained bindings (`"base"` → decorator). Open-generic decorators expressible with markers — **proven in our PoC**, applied automatically to every handler binding.
* Fallback registration: exact-match binding beats marker binding — **proven** (`IValidator<PingQuery>` overrides `IValidator<TT>` → `NullValidator<TT>`).
* **Full compile-time graph validation** (missing dependency = build error) — the strongest `Verify()` equivalent; closest to the "no magic" philosophy.
* Scoped lifetime via child compositions; `Func<T>`/`Lazy<T>` built in.
* MEDI bridge: `Pure.DI.MS` `ServiceProviderFactory<TComposition>` with two-way resolution (framework services resolved from `IServiceProvider`), AOT-published `MinimalWebAPI` sample exists.
* Gaps: no assembly scanning (explicit bindings only → Ark would generate them, see §7), no runtime late-bound registration, decorator chains need tags on ctor params or factory lambdas.

### 5.2 MEDI (+ Injectio 6.1.0 registration/decorator generator) — MIT, active

* MEDI works on NativeAOT (reflection-safe `CallSiteRuntimeResolver`; IL-emit engines disabled) and is the ecosystem default; ASP.NET Core AoT ships on it.
* Injectio generates `IServiceCollection` registrations from attributes (`[RegisterSingleton]`, `[RegisterDecorator]`, keyed services, ordering) — compile-time replacement for scanning (F5).
* Open-generic decoration works **only over closed registrations** (MEDI cannot decorate purely open-generic registrations — factory limitation); attribute-per-handler naturally produces closed registrations, so the Ark pipeline pattern works — **proven in our PoC**.
* **NativeAOT pitfall found by our PoC**: Injectio closes decorator types at runtime via `ActivatorUtilities` → crashes on AOT unless the closed decorator instantiations are rooted. Fix (proven): generated `[DynamicDependency(PublicConstructors, …)]` roots + explicit construction for value-type generic args. An Ark-owned generator must emit these.
* No compile-time graph validation (only runtime `ValidateOnBuild`/`ValidateScopes`), no captive-dependency diagnostics, no `Func<T>`/`Lazy<T>`. Weakest on the "no magic" axis; strongest on ecosystem/integration (F10 becomes a non-issue: one container, no cross-wiring at all).

### 5.3 StrongInject 1.4.4 (YairHalberstadt/stronginject) — MIT, **unmaintained since 2022**, ~870★

* Feature-wise the best SimpleInjector match: real open-generic registration, `[RegisterDecorator]`, **generic `[DecoratorFactory]` methods** (true open-generic decoration), optional-parameter fallback (≈ `!c.Handled`), compile-time errors, `Func<T>`/`Owned<T>`, async resolution.
* Disqualified as a dependency by abandonment (last stable May 2022, pre-dates .NET 7+ AoT tooling, no keyed services). Only viable as a **fork/adoption** — see §8 Q4.

### 5.4 Jab 0.12.0 (pakrym/jab) — disqualified

No open generics, no decorators, no conditional registration; dormant since Sep 2025. Fails F1/F3 outright.

### 5.5 Others

* **Scrutor**: runtime reflection scanning/decoration — not AoT-viable.
* **MrMeeseeks.DIE** (7★), **ThunderboltIoc** (56★), **SourceInject** (81★): negligible traction/dormant — unacceptable adoption risk.
* **Autofac/Ninject/Lamar**: runtime reflection/IL — no AoT story.
* **No Microsoft source-generated MEDI exists** as of mid-2026.

### 5.6 Forking SimpleInjector

* Legally trivial (MIT), technically a **rewrite**: the engine builds `Expression` trees from reflection everywhere; AoT needs both pervasive trimming annotations and a source generator that roots/monomorphizes every closed generic the container may build at runtime (decorators, collections, variance handlers). That generator would have to *see the registrations*, but SimpleInjector registrations are runtime code — a compile-time analyzer cannot reliably recover `Register(typeof(X), assemblies)` results. You would end up designing a new compile-time DSL anyway, i.e. building Pure.DI/StrongInject with the SimpleInjector API surface. Maintenance burden lands entirely on Ark.
* Verdict: **not recommended** unless the clarifications in §8 rule everything else out.

### 5.7 Comparison vs Ark features (F1–F12)

| Feature | Pure.DI | MEDI+Injectio(+Ark generator) | StrongInject (fork) | SimpleInjector fork |
|---|---|---|---|---|
| F1 open-generic decorators | ✅ proven (markers+tags) | ✅ proven (closed regs; AoT needs generated roots) | ✅ by design | ✅ but AoT = rewrite |
| F3 conditional fallback | ✅ proven (exact beats marker) | ✅ proven (`TryAdd` open generic) | ✅ optional-param pattern | ✅ |
| F4 runtime dispatch | ✅ proven (`Resolve(Type)` over roots) | ✅ proven (`GetRequiredService(Type)`) | ⚠️ roots must be declared | ✅ |
| F5/F6 scanning + collections | ⚠️ needs Ark generator to emit bindings | ✅ attributes = compile-time scanning | ⚠️ modules only | ✅ |
| F7 verification/diagnostics | ✅✅ compile-time graph | ⚠️ runtime `ValidateOnBuild` only | ✅ compile-time | ✅ |
| F8 scopes | ✅ proven (child composition) | ✅ proven (`IServiceScope`) | ⚠️ `Owned<T>` model differs | ✅ |
| F9 Func/Lazy | ✅ proven | ❌ (needs Ark shim) | ✅ | ✅ |
| F10 ASP.NET Core integration | ✅ proven (`Pure.DI.MS`, §6.7; MVC-trimming limit is container-independent) | ✅✅ native (no cross-wiring at all) | ⚠️ stale sample | ⚠️ port needed |
| AoT proof | ✅ our PoC + upstream samples | ✅ our PoC (with rooting fix) | ❌ unverified | ❌ |
| Maintenance risk | low (active, single-org) | lowest (MS + active Injectio; Injectio replaceable by Ark generator) | **highest** (adopt abandonware) | **highest** (own a container) |
| "No magic" philosophy fit | ✅✅ | ⚠️ runtime graph, best-effort | ✅ | ✅ |

---

## 6. Feasibility proof

Two assert-based PoCs in [`evaluations/`](../evaluations/README.md), both **passing on CoreCLR and on published NativeAOT binaries** (linux-x64, .NET 10). Each replicates the Ark.Tools.Solid pipeline: two query handlers (`string` and value-type `int` results), a validation decorator resolving `IValidator<TQuery>` with `NullValidator<>` fallback + one specific validator, a second (audit) decorator, scoped lifetime, collection resolution, and mediator dispatch by runtime `Type`.

Key empirical findings:

1. **`dynamic` dispatch in `SimpleInjector*Processor` must go regardless of container** — replaced in both PoCs by a default-interface-method bridge (`IQueryHandlerBase<TResult>`), AoT-safe and allocation-free.
2. `typeof(IQueryHandler<,>).MakeGenericType(q, r)` is AoT-safe **iff** the closed instantiation exists statically (it does — the handler implements it). IL3050 warning remains; a generator emitting a `Dictionary<Type, Func<object>>` root map would silence it.
3. **MEDI+Injectio on AoT fails out-of-the-box** for open-generic decorators (`ActivatorUtilities` cannot find ctor of `ValidationDecorator<PingQuery,string>`): metadata was trimmed. Proven fix (what an Ark generator must emit): `[DynamicDependency(PublicConstructors, typeof(Decorator<Q,R>))]` per closed pair, **plus** an explicit construction for value-type generic arguments (`int` results) since those need real native instantiations, not just metadata.
4. **Pure.DI handles the generic constraint `TQuery : IQuery<TResult>`** only via a custom marker (`[GenericTypeArgument] interface TTQuery : IQuery<TT>`); the stock `TT1/TT2` markers fail to compile against constrained interfaces. Works, but must be codified in guidelines.
5. Pure.DI compile-time verification caught every deliberately-broken graph during PoC development (missing validator binding = build error) — behaviour equivalent to `Verify()` but earlier.
6. **Trimmed self-contained deployment** (the actual ambition per §8.1: `PublishTrimmed=true`, `TrimMode=full`, self-contained linux-x64, .NET 10) — both PoCs **pass all checks**:
   * **Pure.DI: zero trim warnings** — the generated composition is plain constructor calls, nothing for the trimmer to break. Startup 0.09 s / ~20 MB RSS.
   * **MEDI+Injectio: works only thanks to our generated roots** (§6.3) and still emits `IL2026` from Injectio's `DecorateOpenGeneric` (`RequiresUnreferencedCode`) — i.e. trim-*unsafe by declaration*, kept alive by side-channel annotations an Ark generator must always emit correctly. Startup 0.20 s / ~22 MB RSS.
   * Output size is equal (~23 MB, dominated by the runtime); the differentiator for the "trimmed + fast startup" goal is warning-free trimming and startup time, both favouring Pure.DI.
7. **ASP.NET Core cross-wiring on Pure.DI (F10)** — `evaluations/PureDiWeb.Evaluation`, a Kestrel app that self-checks over real HTTP:
   * `Pure.DI.MS` `ServiceProviderFactory<Composition>` works as advertised: **controller activation from the composition** (`.Roots<ControllerBase>()` + `AddControllersAsServices()`), health checks resolving composition services, minimal-API endpoints resolving the decorated handler pipeline via `IServiceProvider`, and two-way wiring (framework `ILogger<T>` injected into composition-managed handlers). All pass on CoreCLR.
   * **Hard container-independent finding: MVC controllers cannot run trimmed at all.** `AddControllers()` is `[RequiresUnreferencedCode]` ("MVC does not currently support trimming or native AOT"); discovery uses `Assembly.DefinedTypes` (trimmed → 404); even rooting the app assembly, `MapControllers()` throws `NotSupportedException` ("`IsConvertibleType` is not initialized when `Microsoft.AspNetCore.Mvc.ApiExplorer.IsEnhancedModelMetadataSupported` is false") under `TrimMode=full` on .NET 10. **The trimmed web story is Minimal APIs** — the trimmed PoC path (minimal API + health checks + composition pipeline) passes with 0.92 s including full HTTP self-checks. Consequence: `Ark.Tools.AspNetCore`'s controller-based `ArkStartupWebApi*` cannot be the trimmed-deployment surface regardless of container choice; a minimal-API-based startup flavour is a prerequisite and belongs in the migration plan (§7).

---

## 7. Recommended solution

§8 clarifications (2026-07-02) resolved the decision inputs: the ambition is **trimmed deployment with very low startup times** (AoT is just one vehicle), runtime startup validation is sufficient (compile-time is a nice-to-have), StrongInject adoption is out, dual-container transition is fine, compile-time source-generated scanning is fine (no runtime plugin loading), `Func<T>` auto-factories may be dropped for explicit registration but **fallback registration (F3) is non-negotiable**.

**Decision: two-layer strategy with Pure.DI as the primary AoT/trimming container.**

1. **Decouple Ark.Tools from the container now (container-agnostic core).**
   * Replace `dynamic` dispatch in `Ark.Tools.Solid.SimpleInjector` processors with the DIM-bridge pattern (proven in PoCs) — benefits SimpleInjector users immediately and is a prerequisite for any trimmed deployment.
   * Keep `Ark.Tools.Solid.SimpleInjector` for existing CoreCLR users (Q5: side-by-side transition approved); introduce `Ark.Tools.Solid.PureDI` (name TBD) alongside.
2. **Pure.DI as the trimming/AoT container.** Rationale against the clarified goals:
   * *Trimmed deployment, low startup* (Q1): Pure.DI is the only candidate that trims **warning-free** — generated constructor calls leave nothing for the trimmer to guess about (§6.6). The MEDI+Injectio path is trim-unsafe by declaration (`IL2026`) and only survives via Ark-generated `DynamicDependency` roots — a permanent correctness liability exactly where trimming bugs are hardest to detect. Startup is also measurably faster (no runtime call-site graph construction).
   * *Verification* (Q2): runtime validation suffices, and Pure.DI delivers the compile-time nice-to-have for free.
   * *Fallback is mandatory* (Q7): proven in the PoC (exact-match binding beats `TT`-marker binding — the `RegisterConditional(..., !c.Handled)` equivalent). On MEDI, open-generic `TryAdd` fallback also works but interacts with the decorator rooting problem above.
   * *Scanning via source generator* (Q6): approved — Ark owns a small generator that emits Pure.DI bindings for handlers/validators found at compile time, replacing `GetTypesToRegister`. This generator is needed on **either** path; on Pure.DI it emits only bindings, on MEDI it must also emit trim roots.
   * *`Func<T>`* (Q7): supported natively by Pure.DI anyway; explicit registration remains the guideline.
3. **MEDI-native remains the integration fallback.** ASP.NET Core hosting resolves framework services from MEDI regardless; `Pure.DI.MS` bridges the two. The cross-wiring PoC (§6.7) proved controller activation, health checks and minimal APIs all resolve from the Pure.DI composition — no blocking friction found. It also proved that **MVC itself cannot run trimmed** (container-independent), so the trimmed-deployment surface of `Ark.Tools.AspNetCore` must be a minimal-API startup flavour; the existing controller-based `ArkStartupWebApi*` stays CoreCLR-only.

Explicitly rejected: forking SimpleInjector (§5.6), StrongInject (Q4: no), Jab (missing critical features).

Next steps, in order: (a) Rebus `IHandlerActivator` PoC; (b) Ark scanning-generator prototype; (c) minimal-API `ArkStartup` flavour design (per §6.7); (d) migration plan — needs the Q3 answer (§8.3).

---

## 8. Clarifications received (2026-07-02)

Answers from @AndreaCuneo on the PR; recorded here so the decision trail is self-contained.

1. **Scope of AoT ambition** → *"ambition is trimmed deployment with very low startup times. AoT is just a way to achieve it."* Target is `PublishTrimmed` + fast startup; NativeAOT is optional. Both PoCs re-verified under `TrimMode=full` (§6.6).
2. **Compile-time verification** → *"runtime at-startup validation is sufficient, but compile-time verification is a much interesting nice-to-have addition."* Not a hard requirement; Pure.DI provides it anyway.
3. **Migration tolerance** → *"unclear, explain"* — **still open**, expanded below (§8.3).
4. **StrongInject adoption** → **no**. Ruled out.
5. **Dual-container transition** → **yes**. `Ark.Tools.Solid.SimpleInjector` (CoreCLR) and the new trimming-ready package ship side-by-side for several versions.
6. **Compile-time-visible handlers** → **yes**. *"source generator can be used for scanning helpers at compile time. there is no need for runtime plugin-load."* Runtime assembly loading of handlers is out of scope.
7. **`Func<T>`/variance extensions** → used, but replaceable via source generation. *"auto `Func` factory registration can be dropped for explicit registration; Fallback cannot be dropped."* → F3 (fallback/conditional registration) is **non-negotiable**; F9 auto-factories are droppable.

### 8.3 Q3 expanded: what "migration tolerance" means

Today an application's composition root looks like this (imperative, runtime registration — `samples/.../ApiHost.cs`):

```csharp
container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(PolicyAuthorizeQueryDecorator<,>));
container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), c => !c.Handled);
container.Register(typeof(IQueryHandler<,>), Assembly.GetExecutingAssembly());
```

Every source-generated container needs the registrations to be *statically analyzable*, so this exact API shape cannot survive. The question is **how different the replacement is allowed to look** in application code. Two ends of the spectrum:

* **(a) New declarative surface (cheaper for Ark, bigger app diff).** Applications rewrite their composition root into whatever the chosen tool dictates — a Pure.DI `partial class` + fluent `DI.Setup(...)` DSL, and/or attributes on handlers (`[QueryHandler]`). Migration = rewrite of each app's `ApiHost` registration section (typically 50–150 lines per app), mechanical but manual.
* **(b) Near-source-compatible facade (costlier for Ark, near-zero app diff).** Ark ships an API that *looks like* today's `container.Register*` calls, but is actually a compile-time DSL interpreted by an Ark-owned generator (calls must be literal — no loops/conditionals around registrations). Preserves muscle memory and keeps diffs tiny, but Ark owns a mini-language + analyzer forever, and the "looks imperative, is actually compile-time" duality is itself a source of magic/confusion.

Recommendation embedded in §7 assumes **(a)** — new declarative surface with an Ark scanning generator to keep per-app boilerplate minimal. **Please confirm (a) is acceptable, or state that (b)-style source compatibility is required.**

---

## 9. Sources

* SimpleInjector issues [#1013](https://github.com/simpleinjector/SimpleInjector/issues/1013), [#972](https://github.com/simpleinjector/SimpleInjector/issues/972), [#914](https://github.com/simpleinjector/SimpleInjector/issues/914), [#264](https://github.com/simpleinjector/SimpleInjector/issues/264)
* Pure.DI: https://github.com/DevTeam/Pure.DI (generics: `readme/generics.md`, decorator scenario, `samples/ShroedingersCatNativeAOT`, `Pure.DI.MS`)
* Injectio: https://github.com/loresoft/Injectio (README "Open-generic decoration" caveat)
* Jab: https://github.com/pakrym/jab · StrongInject: https://github.com/YairHalberstadt/stronginject
* MEDI AoT internals: `dotnet/runtime` `ServiceProvider.cs`, `CallSiteFactory.CreateOpenGeneric` (value-type caveat), `ILEmitResolverBuilder` (`RequiresDynamicCode`)
* Local proofs: [`evaluations/`](../evaluations/README.md)
