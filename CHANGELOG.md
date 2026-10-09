# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Warning `ARKMSG029` for a `[Message]` or `[Event]` contract with no explicit name whose type name is only a version, such as a nested `V1`, because its default logical name (`ark.v1`) is meaningless on the wire and collides with other such contracts (`ARKMSG020`). Set `Name` on the attribute; the default naming rule is unchanged.
- `ArkAdaptiveSampler` has a constructor that accepts a `TimeProvider`, so tests can control sampling time.
- `ArkGenerateComplianceSqlStandalone` returns the generated `policy-*.compliance.sql` files, so database projects can collect them without passing global properties. See the updated database project example in the SQL policies guide.
- `InMemoryMessagingTransport.GetPendingCount` reports unsettled deliveries per queue, so tests can wait for in-memory messaging to drain.
- Native messaging processes request contracts (`IRequest<TSelf, TResponse>`) listed in a participant's `Processes`: the request handler runs and the response is discarded.
- `MessagingFailFastReason.HandlerRejected` lets a second-level handler dead-letter a message it gives up on, instead of returning it to retries.
- Fluent native messaging composition accepts an explicit resource-management seam (`UseResourceManagement`) and provisions a receiver's queue, topics and forwarding subscriptions, so Service Bus hosts outside Azure Functions can self-provision under `CreateIfMissing`.
- The Minimal API generator reports `ARKMF059` when a `GET` or `DELETE` contract has a property that cannot be bound from the route or query string, such as a complex object, a collection of complex objects, an attachment, an `[HttpBody]` property, a property that would be silently dropped because it is not marked `[HttpRoute]` or `[HttpQuery]` while other properties are, or a property that ASP.NET Core would infer as a body (for example a `List<T>`, a dictionary or a NodaTime type) and that would make the endpoint fail at startup. Its diagnostics for contracts in a referenced assembly are reported at the `MapArkEndpoints` call that discovers them. The Azure Functions generator reports `ARKMF059` for a `GET`, `HEAD` or `DELETE` property that is neither a route nor an `[HttpQuery]` property, which it used to ignore silently.

### Changed

- Lower per-call overhead on hot paths: root-span sampling in `ArkAdaptiveSampler`, SQL span filtering and query labels, ResourceWatcher activity tags, polymorphic JSON reads, `ToObject<T>` on `JsonElement` and `JsonDocument`, polymorphic `DataTable` shredding, the MVC ETag filter, Minimal API ETag and MessagePack negotiation, Storage Queue header decoding, gRPC business-rule errors, and authorization logging when Trace is off.
- The compliance analyzers do less work per operation, which shortens builds of projects that enable them.
- `Ark.Tools.Sdk` copies `appsettings*.json`, `reqnroll*.json` and `testconfig.json` to the output with `CopyToOutputDirectory=IfDifferent` instead of `Always`. An edited output copy is still restored, but unchanged files are no longer copied on every build, so no-op builds and the Visual Studio up-to-date check can skip the project. Requires MSBuild 17.13 or later.
- `EvolvableEnum` conversions (`FromValue`, the implicit and explicit operators, and `Value`) no longer allocate, and writing names to JSON and Dapper parameters allocates less.
- The MediatorFramework source generators (Minimal API, gRPC, Rebus, MCP, messaging network and Azure Functions) reuse their previous results when code in other files changes, so editing projects that use them is faster in the IDE. The generated code is unchanged.
- **Breaking:** exposing a contract through gRPC whose request, response or stream item is not a protobuf contract (for example `IAsyncEnumerable<int>`) is now a build error (`ARKMF058`). Such a method used to be generated and then skipped at startup with only a warning. A closed generic contract such as `Page<Book>` is also an error, because it cannot be exported to `.proto`; use a non-generic contract type.
- **Breaking:** under `CreateIfMissing`, a fluent messaging receiver on a transport without built-in resource management (Service Bus) must call `UseResourceManagement` or set the network to `External`; otherwise composition fails.
- **Breaking:** the `MessagingDispatch` delegate, the type of the `dispatch` parameter of the public `MessagingDispatcher` constructor, and the `IMessagingPipelineProcessor.ProcessIncomingAsync` terminal also receive the scoped `IRequestProcessor`, so messaging hosts must register `IRequestProcessor` (`AddArkSolidProcessors` does); a receiving host without it fails at startup.
- **Breaking:** `MessagingParticipantDescriptor` gained constructor parameters, so code compiled against the previous constructor must be rebuilt.
- **Breaking:** `[GrpcMethod]` and `[GrpcService]` moved from `Ark.Tools.MediatorFramework.Grpc` to `Ark.Tools.MediatorFramework`, with the namespace unchanged, so a contracts project no longer needs an ASP.NET Core package to declare gRPC exposure. Projects that reference the base package keep compiling; binaries built against the old assembly must be rebuilt.

### Fixed

- `AddArkAzureMonitorOpenTelemetry` keeps `ArkAdaptiveSampler` when an Application Insights connection string is configured. `UseAzureMonitor` used to replace it with its own sampler, so `ApplicationInsights:ArkAdaptiveSampler` settings and the noise pre-filter had no effect in production.
- The API-surface snapshot records the fields of contracts marked only with `[Message]` or `[Event]`, so renaming or removing a field of such a message fails the API-surface gate again. To accept the new entries, build with `-p:EmitCompilerGeneratedFiles=true`, review the snapshot in the generated `ArkApiSurface.g.cs` under `obj/`, and copy it over `ArkApiSurface.txt`. `ArkApiSurface.current.txt` is not refreshed while ARKAPI002 fails the build.
- Azure Functions Service Bus hosts read an optional `<connection key>:administrationConnectionString` setting for resource provisioning, so trigger apps can provision against the local Service Bus emulator, which serves administration on a separate port.
- `ArkAdaptiveSampler` measures elapsed time with a monotonic clock, so system clock changes no longer distort trace rate limiting.
- Disposing a service provider after the messaging outbox processor has started no longer throws `ObjectDisposedException`.
- A Service Bus transport selected with `UseTransport(transport => transport.UseServiceBus(client))` is now disposed, together with its client, when the service provider is disposed. It used to stay open after host shutdown. A transport passed with `Use(transport)` or `UseTransport(transport)` is still owned by the caller.
- Exported gRPC `.proto` files import only the files they use, so clients that treat warnings as errors no longer fail on unused imports.
- Messaging participants that process or subscribe to several contracts now compile.
- An `[ArkRebusHost]` over a participant that subscribes to events now compiles: the generated `SubscribeAsync` called a Rebus `BusExtensions` class that does not exist.
- Generated messaging stream dispatch compiles in projects without implicit usings.
- Undispatchable messaging contracts are reported at build time: an event (declared, published, or subscribed) that is not an `ICommand<TSelf>`, including a request (`ARKMSG018`), and a processed contract that is neither a command nor a request (`ARKMSG027`).
- A messaging participant that lists the same contract twice in `Processes`, `Publishes` or `Subscribes` is reported at build time (`ARKMSG028`) instead of failing at startup.
- Generated Minimal API `GET` and `DELETE` endpoints with `AcceptsMessagePack = true` no longer read the request body, so a plain `GET` without content no longer fails. They bind from the route and query string and negotiate MessagePack for the response only, and OpenAPI no longer lists a request body for them. These endpoints now apply the response `[ETag]` handling of plain endpoints, so `If-None-Match` can return `304 Not Modified`, and startup no longer validates a MessagePack formatter for their request types.
