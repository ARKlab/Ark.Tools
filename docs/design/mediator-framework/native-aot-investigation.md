# Native AOT investigation

Prototype investigation, October 2026: can an end-to-end Mediator Framework
application (HTTP API plus messaging) run as Native AOT, ideally on Azure
Functions, without the outbox? A first prototype answered the Azure Functions
question; [`samples/Ark.MediatorFramework.AotSample`](../../../samples/Ark.MediatorFramework.AotSample/README.md)
replaced it with a Minimal API, outbox processor and receiver, all Native AOT, over SQL Server with Dapper.AOT.
The Core sample's Functions hosts publish ReadyToRun, the recommended Functions path below.

All results come from `linux-x64` publishes with .NET SDK 10.0.401, Microsoft.Azure.Functions.Worker 2.52.0,
Azure Functions Core Tools 4.15.2, Dapper.AOT 1.1.0, Microsoft.Data.SqlClient 7.1.1, and the repository's SQL Server,
Service Bus emulator and Azurite.

## Outcome

| Host | Publish | Result |
| --- | --- | --- |
| ASP.NET Core Minimal API + generic host receiver | Native AOT | **Works end to end**: HTTP → handler → `IBus.Send` → Service Bus → receiver → handler. No Ark.Tools trim or AOT warnings |
| The same with the SQL outbox and an outbox processor (`AotSample`) | Native AOT | **Works end to end**: HTTP → handler → SQL row and outbox row in one transaction → outbox processor → Service Bus → receiver → SQL update. Data access through Dapper.AOT, composition through Microsoft DI |
| Azure Functions isolated worker (HTTP and Service Bus triggers) | Self-contained, trimmed, ReadyToRun | **Works end to end**, with the build workarounds below |
| Azure Functions isolated worker | Native AOT | **Blocked by the Functions worker**: it publishes and the host launches it, but the worker throws in its own startup |

Minimal API startup on the same machine (first HTTP response after process start, median of 5):

| Build | First response | RSS | Deployed size |
| --- | --- | --- | --- |
| Native AOT | ~80 ms | ~43 MB | 28 MB executable, no runtime needed |
| JIT, framework-dependent | ~585 ms | ~81 MB | 33 MB plus the shared runtime |

These numbers are indicative, not a benchmark. The `AotSample` WebInterface answers its first request about 0.5 s after
process start, including opening SQL Server and checking the schema; its three executables are 37, 31 and 21 MB and run
in 45 to 65 MB of RSS.

## Ark.Tools gaps

### Fixed in this investigation

- **Generated Azure Functions HTTP endpoints used reflection-based JSON.** They called
  `HttpRequestJsonExtensions.ReadFromJsonAsync<T>(request, ct)` and `Results.Json(value, statusCode)`, both
  `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. They now call `ArkAzureFunctionsHttp.ReadJsonAsync<T>` and
  `ArkAzureFunctionsHttp.Json<T>`, which resolve `JsonTypeInfo<T>` from the configured `JsonOptions`.
- **No Microsoft DI processor.** `Ark.Tools.Solid` now ships `ServiceProviderQueryProcessor`,
  `ServiceProviderRequestProcessor` and `ServiceProviderCommandProcessor`, registered by
  `AddArkSolidServiceProviderProcessors()`. Their typed overloads resolve the closed handler without reflection;
  decorators are applied at registration, as the [composition comparison](aot-di-comparison.md) recommends.

### Open

| Gap | Impact | Proposal |
| --- | --- | --- |
| No registration helper for decorated handlers in Microsoft DI | Each AOT application writes the generic helper of the [composition comparison](aot-di-comparison.md) | Ship `AddArkQuery`/`AddArkRequest`/`AddArkCommand` with the standard decorator chain once the chain is agreed; a registration generator can emit the per-handler lines later |
| `Ark.Tools.Outbox.SqlServer` and `Ark.Tools.Dapper` call Dapper from their own assemblies, and with `CommandDefinition` | Dapper.AOT rewrites only the calls of a project that opts in, and never a `CommandDefinition` call (DAP057), so the Ark SQL outbox and data helpers stay on reflection-based Dapper | An AOT-safe SQL outbox in Ark.Tools would opt in to Dapper.AOT (a new dependency, not approved yet) or use plain ADO.NET, which it barely needs: four statements. The sample implements the outbox itself |
| `Ark.Tools.AspNetCore.ProblemDetails`, referenced by `Ark.Tools.MediatorFramework.MinimalApi`, references `Ark.Tools.Sql.SqlServer` | Every Minimal API host carries Microsoft.Data.SqlClient and Dapper, with their AOT warnings, even without a database | Move the SQL exception mapping to a separate package |
| The Functions source generators cannot see each other's output: the Worker SDK metadata and executor generators ignore a `[Function]` the Ark generator emits in the same compilation | A `MessagingFunctionsHost` declared in the app is not indexed, and the generated `DirectFunctionExecutor` does nothing for it. Not specific to AOT: the Core Functions Processor's generated metadata also lacks its Service Bus trigger (observed in the generated provider; not run under `func`) | Declare `MessagingFunctionsHost` in a referenced library and set `FunctionsInDependencies`, as `HttpHost` already is. Document it, or emit a diagnostic when it is declared in an executable |
| The Worker SDK generator also indexes the HTTP functions of a referenced library even without `FunctionsInDependencies` | A trigger app exposes the HTTP API of any library it references | Keep `HttpHost` and `MessagingFunctionsHost` in separate libraries |
| `Ark.Tools.MediatorFramework.Messaging` always references MessagePack, and registers `StandardResolver` by default | MessagePack's dynamic resolvers use `Reflection.Emit`: publish warnings (IL2104/IL3053) even for JSON-only apps, and a runtime failure if a MessagePack participant is used | Register the MessagePack codec only when a participant selects it, and require a source-generated resolver under AOT |
| `ArkAzureFunctionsHttp.WriteJsonStreamAsync` serializes with the default options, not the host's | Streaming responses ignore the configured naming and fail when reflection-based JSON is disabled | Resolve `JsonTypeInfo<IAsyncEnumerable<T>>` from `JsonOptions`, like `Json<T>` |
| The Functions generator drops the nullable annotation of a query result (`IQuery<V1, Greeting?>` fails with CS8631) | A nullable result, which maps to 404, cannot be declared honestly | Emit the annotated type argument |
| `ConfigureArkDefaults`, the NodaTime range converters, the `EvolvableEnum` converter factory and Vogen's numeric converters are `[RequiresDynamicCode]` | Applications with these types need explicit converters in a source-generated context | Known and documented ([value objects](../../mediator-framework/value-objects-best-practices.md)); a per-type, AOT-safe converter set would remove the step |
| SimpleInjector, Dapper, Application Insights and MVC are not AOT-safe | The existing Core sample cannot be published as Native AOT without replacing its container, data access and telemetry | Out of scope for the framework; Microsoft DI, Dapper.AOT and OpenTelemetry are the usual replacements, as in `AotSample` |

### Third-party findings

| Finding | Workaround |
| --- | --- |
| Microsoft.Data.SqlClient throws `Globalization Invariant Mode is not supported` when it opens a connection | Do not set `InvariantGlobalization`; ship ICU in the image |
| Dapper.AOT 1.1.0 binds a `byte[]` parameter as a list expansion (`PackListParameters`), even with `[DbValue(DbType = Binary)]`, which produces invalid SQL | Plain ADO.NET for that statement |
| Dapper.AOT 1.1.0 silently leaves a call inside a `foreach` body on reflection-based Dapper, with no diagnostic; it fails only at run time | Move the call to a helper method. The publish log shows it: Dapper's `IL2104`/`IL3053` appear only when a call fell back |
| Dapper.AOT reads the SQL and parameters only from the direct overloads, not from `CommandDefinition` (DAP057) | Pass the `CancellationToken` as a member of the parameter object; Dapper.AOT honours it |

## Azure Functions gaps (Microsoft)

Microsoft's position ([azure-functions-dotnet-worker#2428](https://github.com/Azure/azure-functions-dotnet-worker/issues/2428),
January 2025): Functions can run Native AOT apps, but the platform's own cold-start mitigation (placeholders) combined
with ReadyToRun gives better results, so Native AOT is not the recommended path. The first prototype hit these gaps, in
order:

| # | Gap | Workaround tried |
| --- | --- | --- |
| 1 | With `PublishAot` there is no apphost, so the Worker SDK writes `worker.config.json` as `dotnet <app>.dll`, a file Native AOT does not produce | An MSBuild target after publish rewrote both paths to the native executable (the host requires the worker path to exist) |
| 2 | The worker bootstraps by reflection over the app assembly (`Assembly.GetTypes()`, `Activator.CreateInstance` on generated startup types) and activates its middleware by type without annotations | `TrimmerRootAssembly` for the app and the worker assemblies |
| 3 | **Blocker.** `WorkerInformation`'s static constructor calls `FileVersionInfo.GetVersionInfo(Assembly.Location)`; `Location` is empty under Native AOT, so `WorkerHostedService.StartAsync` throws | None. ILC body substitutions cannot return a string or a reference type, and the throw is a `static readonly` initializer. Needs an upstream fix |
| 4 | The worker serializes its own DTOs with reflection-based System.Text.Json (diagnostics, worker indexing, the HTTP proxy) | Trimmed: `JsonSerializerIsReflectionEnabledByDefault=true` with the worker assemblies rooted. Native AOT: would also need upstream source generation |
| 5 | `ConfigureFunctionsWebApplication` registers MVC (`AddMvc`), which is not trimming- or AOT-safe | None; MVC warnings were tolerated |

Gaps 2, 4 and 5 also apply to a trimmed Functions app; the prototype treated the worker's publish warnings as
non-fatal (`WarningsNotAsErrors`). ReadyToRun without trimming avoids them all.

## Recommendations

1. **Azure Functions:** publish ReadyToRun for the target runtime, with placeholders enabled, as the Core sample's
   Functions hosts do. This is the supported fast path; revisit Native AOT when the worker fixes gaps 3 and 4.
2. **Native AOT today:** host the same Application in ASP.NET Core Minimal API and a generic-host receiver, for example on
   Azure Container Apps with scale to zero. The framework already supports it with no Ark.Tools warnings.
3. **Framework work, in order:** optional MessagePack registration; the SQL exception mapping out of
   `Ark.Tools.AspNetCore.ProblemDetails`; an AOT-safe SQL outbox; the decorated-handler registration helpers; the
   streaming JSON helper; nullable query results.
4. **Guard it in CI:** publish `AotSample`'s hosts as Native AOT, as CI already does for
   `tests/Ark.Tools.MediatorFramework.NativeAotApp`, and fail on any Ark.Tools or Dapper warning.
