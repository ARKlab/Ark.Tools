# Native AOT investigation

Prototype investigation, October 2026: can an end-to-end Mediator Framework
application (HTTP API plus messaging) run as Native AOT, ideally on Azure
Functions, without the outbox? The prototype is
[`samples/Ark.MediatorFramework.Sample/Aot`](../../../samples/Ark.MediatorFramework.Sample/Aot/README.md).

All results come from `linux-x64` publishes with .NET SDK 10.0.401, Microsoft.Azure.Functions.Worker 2.52.0,
Azure Functions Core Tools 4.15.2, and the repository's Service Bus emulator and Azurite.

## Outcome

| Host | Publish | Result |
| --- | --- | --- |
| ASP.NET Core Minimal API + generic host receiver | Native AOT | **Works end to end**: HTTP → handler → `IBus.Send` → Service Bus → receiver → handler. No Ark.Tools trim or AOT warnings |
| Azure Functions isolated worker (HTTP and Service Bus triggers) | Self-contained, trimmed, ReadyToRun | **Works end to end**, with the build workarounds below |
| Azure Functions isolated worker | Native AOT | **Blocked by the Functions worker**: it publishes and the host launches it, but the worker throws in its own startup |

Minimal API startup on the same machine (first HTTP response after process start, median of 5):

| Build | First response | RSS | Deployed size |
| --- | --- | --- | --- |
| Native AOT | ~80 ms | ~43 MB | 28 MB executable, no runtime needed |
| JIT, framework-dependent | ~585 ms | ~81 MB | 33 MB plus the shared runtime |

These numbers are indicative, not a benchmark.

## Ark.Tools gaps

### Fixed in this investigation

- **Generated Azure Functions HTTP endpoints used reflection-based JSON.** They called
  `HttpRequestJsonExtensions.ReadFromJsonAsync<T>(request, ct)` and `Results.Json(value, statusCode)`, both
  `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. They now call `ArkAzureFunctionsHttp.ReadJsonAsync<T>` and
  `ArkAzureFunctionsHttp.Json<T>`, which resolve `JsonTypeInfo<T>` from the configured `JsonOptions`.

### Open

| Gap | Impact | Proposal |
| --- | --- | --- |
| No Microsoft DI processor. `IRequestProcessor`, `IQueryProcessor` and `ICommandProcessor` exist only for SimpleInjector, which is not AOT-safe (`AddArkSolidProcessors(container)`). The existing Native AOT smoke test hand-writes one too | Every AOT host must write its own | Ship `ServiceProvider*Processor` (the sample's `ServiceProviderProcessors.cs`) in `Ark.Tools.Solid`, typed overloads only, and an `AddArkSolidServiceProviderProcessors()` registration. Decorators (validation, authorization, logging) would need a generator or explicit registration |
| The Functions source generators cannot see each other's output: the Worker SDK metadata and executor generators ignore a `[Function]` the Ark generator emits in the same compilation | A `MessagingFunctionsHost` declared in the app is not indexed, and the generated `DirectFunctionExecutor` does nothing for it. Not specific to AOT: the Core Functions Processor's generated metadata also lacks its Service Bus trigger (observed in the generated provider; not run under `func`) | Declare `MessagingFunctionsHost` in a referenced library and set `FunctionsInDependencies`, as `HttpHost` already is. Document it, or emit a diagnostic when it is declared in an executable |
| The Worker SDK generator also indexes the HTTP functions of a referenced library even without `FunctionsInDependencies` | A trigger app exposes the HTTP API of any library it references | Keep `HttpHost` and `MessagingFunctionsHost` in separate libraries (the prototype does) |
| `Ark.Tools.MediatorFramework.Messaging` always references MessagePack, and registers `StandardResolver` by default | MessagePack's dynamic resolvers use `Reflection.Emit`: publish warnings (IL2104/IL3053) even for JSON-only apps, and a runtime failure if a MessagePack participant is used | Register the MessagePack codec only when a participant selects it, and require a source-generated resolver under AOT |
| `ArkAzureFunctionsHttp.WriteJsonStreamAsync` serializes with the default options, not the host's | Streaming responses ignore the configured naming and fail when reflection-based JSON is disabled | Resolve `JsonTypeInfo<IAsyncEnumerable<T>>` from `JsonOptions`, like `Json<T>` |
| The Functions generator drops the nullable annotation of a query result (`IQuery<V1, Greeting?>` fails with CS8631) | A nullable result, which maps to 404, cannot be declared honestly | Emit the annotated type argument |
| `ConfigureArkDefaults`, the NodaTime range converters, the `EvolvableEnum` converter factory and Vogen's numeric converters are `[RequiresDynamicCode]` | Applications with these types need explicit converters in a source-generated context | Known and documented ([value objects](../../mediator-framework/value-objects-best-practices.md)); a per-type, AOT-safe converter set would remove the step |
| SimpleInjector, Dapper, Application Insights and MVC are not AOT-safe | The existing Core sample cannot be published as Native AOT without replacing its container, data access and telemetry | Out of scope for the framework; Dapper.AOT and OpenTelemetry are the usual replacements |

## Azure Functions gaps (Microsoft)

Microsoft's position ([azure-functions-dotnet-worker#2428](https://github.com/Azure/azure-functions-dotnet-worker/issues/2428),
January 2025): Functions can run Native AOT apps, but the platform's own cold-start mitigation (placeholders) combined
with ReadyToRun gives better results, so Native AOT is not the recommended path. The prototype hit these gaps, in order:

| # | Gap | Workaround in the prototype |
| --- | --- | --- |
| 1 | With `PublishAot` there is no apphost, so the Worker SDK writes `worker.config.json` as `dotnet <app>.dll`, a file Native AOT does not produce | `FunctionsNativeAot.targets` rewrites both paths to the native executable (the host requires the worker path to exist) |
| 2 | The worker bootstraps by reflection over the app assembly (`Assembly.GetTypes()`, `Activator.CreateInstance` on generated startup types) and activates its middleware by type without annotations | `TrimmerRootAssembly` for the app and the worker assemblies |
| 3 | **Blocker.** `WorkerInformation`'s static constructor calls `FileVersionInfo.GetVersionInfo(Assembly.Location)`; `Location` is empty under Native AOT, so `WorkerHostedService.StartAsync` throws | None. ILC body substitutions cannot return a string or a reference type, and the throw is a `static readonly` initializer. Needs an upstream fix |
| 4 | The worker serializes its own DTOs with reflection-based System.Text.Json (diagnostics, worker indexing, the HTTP proxy) | Trimmed: `JsonSerializerIsReflectionEnabledByDefault=true` with the worker assemblies rooted. Native AOT: would also need upstream source generation |
| 5 | `ConfigureFunctionsWebApplication` registers MVC (`AddMvc`), which is not trimming- or AOT-safe | None; MVC warnings are tolerated in the prototype |

Gaps 1, 2, 4 and 5 also apply to a trimmed Functions app. The prototype treats the worker's publish warnings as
non-fatal (`WarningsNotAsErrors`), so check the publish log for Ark.Tools warnings separately.

## Recommendations

1. **Azure Functions:** publish self-contained, trimmed and ReadyToRun (the prototype's `-p:FunctionsNativeAot=false`),
   with placeholders enabled. This is the supported fast path; revisit Native AOT when the worker fixes gap 3 and 4.
2. **Native AOT today:** host the same Application in ASP.NET Core Minimal API and a generic-host receiver, for example on
   Azure Container Apps with scale to zero. The framework already supports it with no Ark.Tools warnings.
3. **Framework work, in order:** the Microsoft DI processors; a diagnostic or documentation for `MessagingFunctionsHost`
   placement; optional MessagePack registration; the streaming JSON helper; nullable query results.
4. **Guard it in CI:** publish the prototype's Web and Worker hosts as Native AOT, as CI already does for
   `tests/Ark.Tools.MediatorFramework.NativeAotApp`, and fail on any Ark.Tools warning.
