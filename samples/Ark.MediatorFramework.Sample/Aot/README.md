# Native AOT prototype

A minimal Mediator Framework application, published as Native AOT: an HTTP API that stores a greeting and sends a
command over Azure Service Bus, and a processor that handles it. It has no outbox, no database and no SimpleInjector:
only Microsoft dependency injection and source-generated JSON. The findings are in the
[Native AOT investigation](../../../docs/design/mediator-framework/native-aot-investigation.md).

## Projects

| Project | Role |
| --- | --- |
| `Ark.MediatorFramework.Sample.Aot.Application` | Contracts, handlers, messaging declarations, the source-generated JSON context and Microsoft DI processors. Transport-neutral |
| `Ark.MediatorFramework.Sample.Aot.Web.Api` | Minimal API with generated endpoints; sends to Service Bus. Native AOT |
| `Ark.MediatorFramework.Sample.Aot.Worker.Processor` | Generic host Service Bus receiver. Native AOT |
| `Ark.MediatorFramework.Sample.Aot.Functions.Api` | Azure Functions HTTP triggers (from `HttpTriggers`); sends to Service Bus |
| `Ark.MediatorFramework.Sample.Aot.Functions.Processor` | Azure Functions Service Bus trigger (from `ProcessorTriggers`) |
| `Ark.MediatorFramework.Sample.Aot.Functions.HttpTriggers` | Holds the `HttpHost`: the Worker SDK generators only see functions in referenced assemblies |
| `Ark.MediatorFramework.Sample.Aot.Functions.ProcessorTriggers` | Holds the `MessagingFunctionsHost`, for the same reason |

`FunctionsNativeAot.targets` works around the Azure Functions Worker SDK gaps listed in the investigation.

## Run

Start the emulators from the repository root with `docker compose up -d` and wait for the Service Bus emulator
(`curl http://127.0.0.1:5300/health`).

### Native AOT: Web and Worker

```bash
dotnet publish Ark.MediatorFramework.Sample.Aot.Worker.Processor -c Release -o out/worker
dotnet publish Ark.MediatorFramework.Sample.Aot.Web.Api -c Release -o out/api
(cd out/worker && ./Ark.MediatorFramework.Sample.Aot.Worker.Processor) &
(cd out/api && ASPNETCORE_URLS=http://127.0.0.1:7081 ./Ark.MediatorFramework.Sample.Aot.Web.Api) &

curl -X POST http://127.0.0.1:7081/api/v1/greetings -H 'Content-Type: application/json' -d '{"name":"Customer A"}'
```

The worker logs `Processed greeting <id>`. `GET /api/v1/greetings/{id}` returns the greeting from the Api process.

### Azure Functions: trimmed and ReadyToRun

Native AOT does not start under the Functions host (see the investigation), so publish the Functions apps
self-contained, trimmed and ReadyToRun:

```bash
dotnet publish Ark.MediatorFramework.Sample.Aot.Functions.Processor -c Release -p:FunctionsNativeAot=false -o out/fprocessor
dotnet publish Ark.MediatorFramework.Sample.Aot.Functions.Api -c Release -p:FunctionsNativeAot=false -o out/fapi
```

Copy each project's `local.settings.json.example` to `local.settings.json` in its output folder, then run
`func host start --port 7072` in `out/fprocessor` and `func host start --port 7071` in `out/fapi`. The routes are the
same as the Web Api's. Publish without `-p:FunctionsNativeAot=false` to reproduce the Native AOT failure.
