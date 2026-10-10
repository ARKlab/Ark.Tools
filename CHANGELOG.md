# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- `ToDataTableArk()` shreds Vogen value objects (`[ValueObject<T>]` or `[ValueObject(typeof(T))]`) into a column of their primitive type, so they can be passed in table-valued parameters and `SqlBulkCopy`. Ark.Tools.Core does not depend on Vogen.
- Mediator Framework contracts can use Vogen value objects. They travel as the primitive they wrap in JSON (`ConfigureArkDefaults()` now applies Vogen's converter even through a source-generated `JsonSerializerContext`), in Minimal API and Azure Functions routes and query strings, and over gRPC, where the exported `.proto` declares the primitive and `MapArkGrpcServicesFromAssembly` registers the value object with protobuf-net. Add `AddArkValueObjectSchemas()` to document them in OpenAPI, including collections and route and query parameters, with the same result whether or not Vogen's own OpenAPI mapping is also registered, and call `ValueObjectDapper.Register<TValueObject, TPrimitive>()` for Dapper. Ark.Tools does not depend on Vogen.
- Error `ARKMF060` for a gRPC contract member whose exported `.proto` type differs from what protobuf-net writes: a `Guid` below `CompatibilityLevel.Level300`, or a value object over a primitive with no protobuf scalar.

### Changed

- **Breaking:** the exported gRPC `.proto` declares a `Guid` as `string` instead of `bytes`. protobuf-net writes a `Guid` as its own `bcl.Guid` message, so clients generated from the old schema could not read these fields. Add `[assembly: ProtoBuf.CompatibilityLevel(ProtoBuf.CompatibilityLevel.Level300)]` to the contracts assembly (`ARKMF060` reports a missing one); this changes the wire format of its `Guid` fields to the canonical string.
- `Ark.Tools.MediatorFramework.MinimalApi`: generated endpoints bind route, query and JSON body values in generated code, with the same rules as ASP.NET Core Minimal API, instead of through `RequestDelegateFactory`. They avoid its reflection-based binding, so they are trim-safe; endpoint filters and OpenAPI work as before. `GET` and `DELETE` contracts are bound property by property instead of with `[AsParameters]`. In a trimmed app, add the contract and response types to a `JsonSerializerContext` in the Minimal API JSON options.
- `Ark.Tools.ResourceWatcher.Sql`: trimmed apps that use `SqlStateProvider<TExtensions>` without `ExtensionsJsonContext` now keep the public constructors, properties and fields of a flat extensions type and opt back into reflection-based serialization, so it round-trips instead of failing or losing values. Generic code that passes its own type parameter as `TExtensions` must add the same `[DynamicallyAccessedMembers]` annotation (trim analyzer IL2091). Nested or polymorphic extension types still need `ExtensionsJsonContext` when trimming.

### Fixed

- `AddArkAzureFunctions` no longer adds reflection-based JSON when reflection-based serialization is disabled, as in trimmed and Native AOT apps: it keeps camelCase naming and Vogen value object support, and the source-generated contexts passed to it resolve every type. Publishing such an app no longer reports trimming or AOT warnings from Ark.Tools.
- `ToDataTableArk()` no longer fails in Native AOT apps when the element type, shredded through the reflection fallback, has a nullable value-type member such as `Guid?`, and publishing no longer reports a trimming warning for it.
- `Ark.Tools.MediatorFramework.Grpc`: in a trimmed app, a business rule violation now reaches the client as `FailedPrecondition` with its title and detail. Its extra properties are omitted, with a warning in the log, because reflection-based JSON is disabled. Before, the mapping threw and the client received `Unknown`.
- `Ark.Tools.AspNetCore.OTel`: binding `ApplicationInsights:ArkAdaptiveSampler` settings no longer relies on reflection, so it keeps working in trimmed apps.
- `Ark.Tools.MediatorFramework.Mcp`: generated MCP tools for self-typed contracts (`IQuery<TSelf, TResult>`, `IRequest<TSelf, TResponse>`, `ICommand<TSelf>`) call the processor's typed overloads, so they no longer need reflection and work in trimmed apps. Contracts that implement only `IQuery<TResult>` or `IRequest<TResponse>` still use the reflection-based dispatch.
- A native messaging delivery cancelled while its payload is being deserialized, by host shutdown or by `MaximumHandlerDuration`, is no longer dead-lettered as a malformed payload; it is retried like any other cancelled or timed-out delivery.
- `Ark.Tools.ResourceWatcher.ApplicationInsights`: the `RetrievedAt` telemetry property now holds the resource retrieval instant instead of the state type name, and is omitted when the instant is unknown.
- `Ark.Tools.ResourceWatcher.Sql`: `SqlStateProvider` reads and writes `ModifiedSources` through a source-generated serializer, so it keeps working in trimmed applications. The stored JSON format is unchanged.
- `Ark.Tools.ResourceWatcher.Sql`: `SqlStateProvider.LoadStateAsync` works in trimmed apps; it used to fail because trimming removed the constructors of the row types it reads.

## [7.0.0-beta13] - 2026-10-09

### Added

- Warning `ARKMSG029` for a `[Message]` or `[Event]` contract with no explicit name whose type name is only a version, such as a nested `V1`, because its default logical name (`ark.v1`) is meaningless on the wire and collides with other such contracts (`ARKMSG020`). Set `Name` on the attribute; the default naming rule is unchanged.
- `ArkGenerateComplianceSqlStandalone` returns the generated `policy-*.compliance.sql` files, so database projects can collect them without passing global properties. See the updated database project example in the SQL policies guide.
- `InMemoryMessagingTransport.GetPendingCount` reports unsettled deliveries per queue, so tests can wait for in-memory messaging to drain.
- Native messaging processes request contracts (`IRequest<TSelf, TResponse>`) listed in a participant's `Processes`: the request handler runs and the response is discarded.
- `MessagingFailFastReason.HandlerRejected` lets a second-level handler dead-letter a message it gives up on, instead of returning it to retries.
- Fluent native messaging composition accepts an explicit resource-management seam (`UseResourceManagement`) and provisions a receiver's queue, topics and forwarding subscriptions, so Service Bus hosts outside Azure Functions can self-provision under `CreateIfMissing`.
- The Minimal API generator reports `ARKMF059` when a `GET` or `DELETE` contract has a property that cannot be bound from the route or query string, such as a complex object, a collection of complex objects, an attachment, an `[HttpBody]` property, a property that would be silently dropped because it is not marked `[HttpRoute]` or `[HttpQuery]` while other properties are, or a property, including a route property of a command, that ASP.NET Core would infer as a body (for example a `List<T>`, a dictionary or a NodaTime type) and that would make the endpoint fail at startup. Its diagnostics for contracts in a referenced assembly are reported at every `MapArkEndpoints` call that discovers them.

### Changed

- **Breaking:** Minimal API (`ArkTypeConverterValue<T>`) and Azure Functions resolve the type converter of a route or query value with the trim-safe `TypeDescriptor.GetConverterFromRegisteredType`. A type bound through its `[TypeConverter]` must be registered with `TypeDescriptor.RegisterType<T>()` at startup, before the type is first looked up or its converter is added; for NodaTime types call `NodaTimeConverter.Register()`. Binding an unregistered type throws `InvalidOperationException`.
- **Breaking:** Azure Functions parses route and query values as Minimal API does: enum names are case-sensitive, a `DateTime` is adjusted to UTC, a `DateTimeOffset` without an offset is read as UTC, and an empty value of a nullable type with a parser, such as `?Owner=` for a `Guid?`, fails with `400`; a nullable value type bound through its type converter, such as `Instant?`, still binds `null`. An absent query value ignores the property initializer: a non-nullable one, such as `int Count { get; init; } = 1` or a `string` declared outside a nullable context, fails with `400` as a missing required parameter, and a nullable one binds `null`. Enum names in another case used to be accepted.
- `ArkTypeConverter.TryConvertSafe` throws `InvalidOperationException` naming the missing registration when the target type is not registered, instead of a `TypeInitializationException`.
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
- `Ark.Tools.Auth0` no longer depends on the unmaintained `Polly.Caching.Memory` package. `AuthenticationApiClientCachingDecorator` caches tokens and user info in `Microsoft.Extensions.Caching.Memory` with the same expiration rules.
- **Breaking:** Ark.Tools packages that retry with Polly now depend on `Polly.Core` (the Polly v8 resilience pipeline API) instead of `Polly`. If your code uses the legacy `Policy` API and got `Polly` through an Ark.Tools package, add a direct `Polly` package reference.
- **Breaking:** the Azure Functions generator reports `ARKMF059` for a `GET`, `HEAD` or `DELETE` property that is neither a route nor an `[HttpQuery]` property, which it used to ignore silently.
- **Breaking:** the Azure Functions generator reports `ARKMF059` for a route or `[HttpQuery]` property, with any HTTP verb, that Minimal API cannot bind either: an array or collection bound from the route, or a collection or complex object other than a string collection or an array of a parseable type, such as `int[]`, bound from the query string. Such a value cannot be converted from a string: the request failed with `400` or the property was set to `null`.
- **Breaking:** the Minimal API generator reports `ARKMF059` for a route or `[HttpQuery]` property, with any HTTP verb, whose type cannot be converted from a string: an array of a type without `TryParse`, which made the endpoint fail at startup, or a collection such as `List<int>` or a dictionary, or a complex object, which made every request that carried the value fail with `400`. Use an array of a parseable type, a string collection or a single value instead.

### Fixed

- `Auth0AccessTokenJwtEvents` no longer throws a `JsonException` when a concurrent request's pending cache entry expires or is removed before the user profile is stored; the request continues without the cached profile claims.
- The Minimal API generator reports `ARKMF059` for a route or `[HttpQuery]` property that has no public setter or `init` accessor and is not a constructor parameter, instead of generating code that fails to compile with `CS0200`.
- The Azure Functions generator reports `ARKMF059` for a route or `[HttpQuery]` property that has no public setter or `init` accessor. The function used to ignore it silently, so the property kept its default value, even when it was a constructor parameter.
- `NodaTimeConverter.Register()` (`Ark.Tools.Nodatime`) makes the NodaTime converters visible to trim-safe `TypeDescriptor.GetConverterFromRegisteredType` lookups, even when a NodaTime type was looked up through `TypeDescriptor` before: HTTP route and query binding, the Dapper `OffsetDateTime` handler and JSON dictionary keys. These lookups used to fail with `InvalidOperationException`.
- `AddArkAzureMonitorOpenTelemetry` keeps `ArkAdaptiveSampler` when an Application Insights connection string is configured. `UseAzureMonitor` used to replace it with its own sampler, so `ApplicationInsights:ArkAdaptiveSampler` settings and the noise pre-filter had no effect in production.
- The API-surface snapshot records the fields of contracts marked only with `[Message]` or `[Event]`, so renaming or removing a field of such a message fails the API-surface gate again. To accept the new entries, build with `-p:EmitCompilerGeneratedFiles=true`, review the snapshot in the generated `ArkApiSurface.g.cs` under `obj/`, and copy it over `ArkApiSurface.txt`. `ArkApiSurface.current.txt` is not refreshed while ARKAPI002 fails the build.
- Azure Functions Service Bus hosts read an optional `<connection key>:administrationConnectionString` setting for resource provisioning, so trigger apps can provision against the local Service Bus emulator, which serves administration on a separate port.
- OpenTelemetry failure promotion no longer treats every HTTP 4xx span as a failure. A 4xx span is promoted only when its status is `Error`, so `WebApi4xxAsSuccessProcessor` (now registered before promotion) and application processors registered before the Ark setup can mark expected 4xx responses, such as Azure Storage 404 or 409, as successes. HTTP 5xx and gRPC error codes are still promoted unless the span status is `Ok`.
- Disposing a service provider after the messaging outbox processor has started no longer throws `ObjectDisposedException`.
- A Service Bus transport selected with `UseTransport(transport => transport.UseServiceBus(client))` is now disposed, together with its client, when the service provider is disposed. It used to stay open after host shutdown. A transport passed with `Use(transport)` or `UseTransport(transport)` is still owned by the caller.
- Exported gRPC `.proto` files import only the files they use, so clients that treat warnings as errors no longer fail on unused imports.
- Messaging participants that process or subscribe to several contracts now compile.
- A Minimal API `GET` or `DELETE` endpoint for a command, or for a request or query without route or `[HttpQuery]` properties, no longer binds its `[ServerSet]` properties from the query string. A server-set property of a type that ASP.NET Core cannot bind from a string, such as a class or a NodaTime type, made the endpoint fail at startup.
- Azure Functions binds an `[HttpQuery]` string collection, such as `string[]`, `List<string>`, `IEnumerable<string>` or `IQueryPaged.Sort`, or an array of a parseable type, such as `int[]`, `Guid[]` or an enum array, from every value of the query parameter, and binds an absent one as empty, as Minimal API does. It used to set an interface such as `IEnumerable<string>` to `null` and fail the request for any other collection.
- Azure Functions converts route and query values with the same strategy as Minimal API: a static `TryParse`, `IParsable<T>`, or the registered type converter. A NodaTime value, such as an `Instant` query filter, used to fail the request with `500`; a type with only a static `TryParse` failed every request.
- Generated Minimal API endpoints that reset a non-nullable reference `[ServerSet]` property, such as `string`, no longer raise `CS8625`, which failed builds that treat warnings as errors.
- A Minimal API route or `[HttpQuery]` property whose type implements `IParsable<T>` without a public static `TryParse` method, such as an explicit interface implementation, is now bound by ASP.NET Core. It used to be read through its `TypeConverter`, so every request that carried the value failed with `400`.
- An `[ArkRebusHost]` over a participant that subscribes to events now compiles: the generated `SubscribeAsync` called a Rebus `BusExtensions` class that does not exist.
- Generated messaging stream dispatch compiles in projects without implicit usings.
- Undispatchable messaging contracts are reported at build time: an event (declared, published, or subscribed) that is not an `ICommand<TSelf>`, including a request (`ARKMSG018`), and a processed contract that is neither a command nor a request (`ARKMSG027`).
- A messaging participant that lists the same contract twice in `Processes`, `Publishes` or `Subscribes` is reported at build time (`ARKMSG028`) instead of failing at startup.
- Generated Minimal API `GET` and `DELETE` endpoints with `AcceptsMessagePack = true` no longer read the request body, so a plain `GET` without content no longer fails. They bind from the route and query string and negotiate MessagePack for the response only, and OpenAPI no longer lists a request body for them. These endpoints now apply the response `[ETag]` handling of plain endpoints, so `If-None-Match` can return `304 Not Modified`, and startup no longer validates a MessagePack formatter for their request types.
- The `Ark.Tools.Solid` exception-logging and profiling decorators (`ExceptionLog*Decorator`, `Profile*Decorator`) apply again to queries, requests and commands that implement only `IQuery<TResult>`, `IRequest<TResponse>` or `ICommand`, and to contracts declared as structs; SimpleInjector used to skip them silently. `ExceptionLogQueryDecorator` no longer requires the query to be `IDisposable`, so it now logs failures of every query.

### Security

- A Minimal API `GET` or `DELETE` endpoint that returns an `IArkAttachment`, for a record without route or `[HttpQuery]` properties, no longer lets a client set its `[ServerSet]` properties from the query string. The generated endpoint bound them and never reset them.
- `[PolicyAuthorize]` is enforced again on queries, requests and commands that implement only `IQuery<TResult>`, `IRequest<TResponse>` or `ICommand`. Since the self-referencing interfaces were introduced, `RegisterAuthorization` and `RegisterAuthorizationDecorator` silently skipped these contracts, so their handlers ran without the policy check. Upgrade if any contract with `[PolicyAuthorize]` does not implement `IQuery<TSelf, TResult>`, `IRequest<TSelf, TResponse>` or `ICommand<TSelf>`.

## [7.0.0-beta12] - 2026-10-07

### Added

- `ArkAdaptiveSampler` has a constructor that accepts a `TimeProvider`, so tests can control sampling time.

### Fixed

- `ArkAdaptiveSampler` measures elapsed time with a monotonic clock, so system clock changes no longer distort trace rate limiting.
