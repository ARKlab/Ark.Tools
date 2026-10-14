# Mediator Framework Native AOT sample

> [!WARNING]
> This sample is a prototype and a large work in progress. It exists to explore Native AOT with the Mediator
> Framework: do not use it as a template or reference for production applications.

A slim Mediator Framework application whose every host is published as Native AOT: a Minimal API that stores a book in
SQL Server and, in the same transaction, queues a print command in the outbox; an outbox processor that moves it to Azure
Service Bus; and a processor that prints the book. It has the structure of
[`Ark.MediatorFramework.Sample`](../Ark.MediatorFramework.Sample/README.md) with one contract per kind instead of a full
API. The findings behind it are in the
[Native AOT investigation](../../docs/design/mediator-framework/native-aot-investigation.md).

## Projects

| Project | Role |
| --- | --- |
| `Core/…Core.API` | Contracts: create and get a book, and the `PrintBookCommand` message |
| `Core/…Core.Application` | Handlers, data access, messaging declarations, source-generated JSON and the composition |
| `Core/Hosts/Web/…Web.Hosting` | Messaging composition shared by the three processes |
| `Core/Hosts/Web/…Web.WebInterface` | Minimal API with generated endpoints, `CreateSlimBuilder`, no MVC. Sends through the outbox |
| `Core/Hosts/Web/…Web.OutboxProcessor` | Moves outbox rows to Service Bus |
| `Core/Hosts/Web/…Web.Processor` | Service Bus receiver for `PrintBookCommand` |
| `Core/Hosts/Web/…Web.Tests` | End-to-end test of the three processes, in-process (JIT) with reflection-based JSON off, as in the published apps |

## What differs from the Core sample

- **Microsoft dependency injection, no SimpleInjector.** SimpleInjector does not run under Native AOT.
  `AddArkSolidServiceProviderProcessors()` registers the Ark.Tools.Solid processors, and
  [`HandlerRegistration`](Core/Ark.MediatorFramework.AotSample.Core.Application/Host/HandlerRegistration.cs) wraps
  each handler in the Ark.Tools.Solid decorators with a generic method: one line per handler, and neither the handlers
  nor the decorators know the composition. The
  [composition comparison](../../docs/design/mediator-framework/aot-di-comparison.md) explains the choice.
- **Dapper.AOT, and the outbox in the application.** Dapper.AOT replaces each Dapper call of the Application project
  with generated code. It only rewrites the calls of a project that opts in, so `Ark.Tools.Outbox.SqlServer`, which
  calls Dapper from its own assembly, is not AOT-safe; the sample's
  [`AotDataContext`](Core/Ark.MediatorFramework.AotSample.Core.Application/DAL/AotDataContext.cs) implements the outbox
  itself. A Dapper `IL2104`/`IL3053` warning at publish means a call was left on reflection-based Dapper.
- **Ark.Tools types end to end.** `Book` carries a Vogen `BookId`, an `EvolvableEnum<Genre>`, the five NodaTime types
  Ark.Tools maps for Dapper and an `EmailAddress` sensitive value. Each crosses HTTP JSON and SQL Server and back; the
  Service Bus message carries only the `BookId`. Dapper.AOT ignores `SqlMapper.AddTypeHandler`, which also crashes Native AOT, so
  [`DbValueHandlers`](Core/Ark.MediatorFramework.AotSample.Core.Application/DAL/DbValueHandlers.cs) registers each type
  with `[module: TypeHandler(...)]`, reusing the Ark.Tools handlers. `ApplicationComposition.ConfigureJson` adds the JSON
  context's converters to the HTTP and messaging options, which do not inherit them.
- **No database project.** The WebInterface and the tests create the database and its two tables at startup.
- **JSON only.** Every participant uses the JSON codec with the source-generated `AotJsonContext`.
- **No gRPC or MCP.** The gRPC host is not AOT-safe yet; see the investigation.
- **Separate processes.** Each process composes one participant.

## Run

Start SQL Server, Azurite and the Service Bus emulator from the repository root with `docker compose up -d`, and wait
for the emulator (`curl http://127.0.0.1:5300/health`). The SQL Server connection string is not committed: set
`ConnectionStrings__Sql` to the database the sample owns, for example
`Server=127.0.0.1,1433;Database=ArkAotSample;User Id=sa;Password=<the compose file's password>;TrustServerCertificate=true`.

```bash
dotnet test --project Core/Hosts/Web/Ark.MediatorFramework.AotSample.Core.Web.Tests

for app in WebInterface OutboxProcessor Processor; do
  dotnet publish Core/Hosts/Web/Ark.MediatorFramework.AotSample.Core.Web.$app -c Release -o out/$app
done
(cd out/Processor && ./Ark.MediatorFramework.AotSample.Core.Web.Processor) &
(cd out/OutboxProcessor && ./Ark.MediatorFramework.AotSample.Core.Web.OutboxProcessor) &
(cd out/WebInterface && ASPNETCORE_URLS=http://127.0.0.1:7091 ./Ark.MediatorFramework.AotSample.Core.Web.WebInterface) &

curl -X POST http://127.0.0.1:7091/api/v1/books -H 'Content-Type: application/json' -d '{"title":"Book A"}'
curl http://127.0.0.1:7091/api/v1/books/1   # "printed": true once the processor has run
```

The apps need ICU (`libicu`) at runtime: Microsoft.Data.SqlClient does not support invariant globalization.

Publish warnings come only from third-party assemblies (Microsoft.Data.SqlClient, MessagePack and
System.Configuration.ConfigurationManager); the apps keep `IL2104` and `IL3053` non-fatal for them.
