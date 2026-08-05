# Host setup and composition

The framework deliberately splits application composition from transport wiring.
Your application assembly owns contracts, handlers, validators, policies, and
business services. Your host assembly owns authentication, middleware order,
OpenAPI, gRPC, transport serialization, and generated endpoint mapping.

The sample shows this split in two files:

- `ApplicationComposition.cs` — pure application graph.
- `SampleStartup.cs` and `SampleComposition.cs` — ASP.NET Core and Rebus host wiring.

## Composition responsibilities

| Layer | Owns | Must not own |
| --- | --- | --- |
| Application assembly | Contracts, handlers, validators, policy types, repositories, decorators, clocks | `HttpContext`, `IApplicationBuilder`, gRPC server registration, Rebus transport setup |
| ASP.NET Core host | Authentication, authorization fallback policy, JSON/MessagePack, ProblemDetails, OpenAPI, generated HTTP + gRPC mapping | Business rules or persistence decisions |
| Rebus host | Transport, routing, generated message handlers, message serialization, worker retry behavior | HTTP request parsing or UI concerns |

## Register the application graph first

The sample's `ApplicationComposition.Register(...)` method is the reference
pattern to copy. It performs four jobs:

1. Chooses concrete infrastructure (`SqlGreetingStore` or `InMemoryGreetingStore`).
2. Registers shared singletons such as `IClock` and the document store.
3. Registers validators and a null-validator fallback.
4. Registers handlers and transport-agnostic decorators.

```csharp
public static void Register(Container container, bool useSqlStore = true)
{
    if (useSqlStore)
        container.RegisterSingleton<IGreetingStore, SqlGreetingStore>();
    else
        container.RegisterSingleton<IGreetingStore, InMemoryGreetingStore>();

    container.RegisterSingleton<IClock>(() => SystemClock.Instance);

    var applicationAssembly = typeof(ApplicationComposition).Assembly;
    container.Register(
        typeof(IValidator<>),
        container.GetTypesToRegister(typeof(IValidator<>), new[] { applicationAssembly })
            .Where(type => type.IsPublic),
        Lifestyle.Singleton);
    container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton, c => !c.Handled);

    container.Register<IRequestHandler<CreateGreetingRequest, GreetingResponse>, CreateGreetingHandler>();
    container.Register<IQueryHandler<GetGreetingQuery, GreetingResponse>, GetGreetingHandler>();

    container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(QueryFluentValidateDecorator<,>));
    container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestFluentValidateDecorator<,>));
    container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(OptimisticConcurrencyRetrierDecorator<,>));
}
```

### Decorator order matters

The sample intentionally registers decorators in this order:

| Registration order | Effective behavior |
| --- | --- |
| Validation decorators first | Invalid input is rejected before the core handler mutates anything |
| Auditing decorators before retry | Each attempt still goes through the same transport-agnostic auditing behavior |
| Retry decorator last | Retries wrap the complete application pipeline, not only the core handler |

If you change the order, you change observable behavior. Treat decorator order
as part of application design, not as cosmetic wiring.

## Register transport-agnostic authorization

Contract-level authorization lives with the contract, not with the HTTP route.
The sample registers the authorization services directly in the container:

```csharp
container.RegisterAuthorization();
container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
```

A custom policy type can live in the application assembly:

```csharp
public sealed class RequireScopePolicy : IAuthorizationPolicy
{
    public RequireScopePolicy(string scope)
    {
        Scope = scope;
        var builder = new AuthorizationPolicyBuilder(nameof(RequireScopePolicy));
        builder.AddRequirements(new ScopeAuthorizationRequirement(Scope));
        var policy = builder.Build();
        Name = policy.Name;
        Requirements = policy.Requirements;
    }

    public string Scope { get; }
    public string Name { get; }
    public IReadOnlyList<IAuthorizationRequirement> Requirements { get; }
}

public sealed class RequireScopePolicyAttribute : PolicyAuthorizeAttribute
{
    public RequireScopePolicyAttribute(string scope)
        : base(typeof(RequireScopePolicy), scope)
    {
    }
}
```

The contract then remains transport-neutral:

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings")]
[GrpcMethod("CreateGreeting")]
[GrpcService("Greetings")]
[RequireScopePolicy(ApplicationScopes.GreetingWrite)]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>;
```

## Configure ASP.NET Core services

`SampleStartup.ConfigureServices` is the reference host setup. Each step has a
separate purpose:

| Step | Why it exists | Sample code |
| --- | --- | --- |
| `services.ConfigureAuthentication(configuration)` | Chooses bearer authentication schemes | `AuthenticationEx.cs` |
| `services.AddArkMinimalApiHost(container, ...)` | Sets the secure authorization baseline (default and fallback policies) and bridges Microsoft DI and SimpleInjector | `SampleStartup.cs` |
| `builder.UseArkMinimalApiStartupDiagnostics()` | Captures startup failures and enables detailed hosting diagnostics | `SampleHost.cs` |
| `services.AddArkMinimalApiSecurity()` | Adds Ark security-header policies for API, documentation and gRPC reflection responses | `SampleStartup.cs` |
| `services.AddMessagePackFormatter(...)` | Enables HTTP MessagePack negotiation for contracts that opt in | `SampleStartup.cs` |
| `services.ConfigureHttpJsonOptions(...)` | Applies Ark JSON defaults and source-generated metadata | `SampleStartup.cs` |
| `services.AddArkProblemDetailsExceptionHandler()` | Maps domain exceptions to RFC 7807 | `SampleStartup.cs` |
| `endpoints.MapArkMinimalApiHost()` | Maps the anonymous `/healthCheck` endpoint without enabling HealthChecks UI or history | `SampleStartup.cs` |
| `RuntimeTypeModel.Default.AddNodaTimeSurrogates()` | Enables NodaTime protobuf mappings before gRPC use | `SampleStartup.cs` |
| `services.AddCodeFirstGrpc(...)` | Hosts generated gRPC services and rich error interceptor | `SampleStartup.cs` |
| `services.AddOpenApi("v1", ...)` per version | Publishes one OpenAPI document for each active HTTP API version | `SampleStartup.cs` |

A condensed sample setup:

```csharp
services.AddArkMinimalApiHost(container, options =>
{
    options.UseForwardedPrefix = true;
    options.CrossWireContainer = (container, serviceProvider) =>
        container.RegisterInstance(serviceProvider.GetRequiredService<IHttpContextAccessor>());
    // Invoked after Verify(), while the host starts and before the server accepts requests.
    options.OnContainerVerified = container => container.StartBus();
});
builder.UseArkMinimalApiStartupDiagnostics();
services.AddArkMinimalApiSecurity();

services.AddMessagePackFormatter(messagePackResolver);
services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ConfigureArkDefaults();
    options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        new SampleJsonSerializerContext(new JsonSerializerOptions().ConfigureArkDefaults()),
        new DefaultJsonTypeInfoResolver());
});

services.AddArkProblemDetailsExceptionHandler();
RuntimeTypeModel.Default.AddNodaTimeSurrogates();
services.AddCodeFirstGrpc(options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());
services.AddOpenApi("v1", ConfigureOpenApi);
services.AddOpenApi("v2", ConfigureOpenApi);
```

## Configure the middleware pipeline in the right order

Order is observable. The sample uses this sequence:

| Order | Middleware | Why |
| --- | --- | --- |
| 1 | `UseArkMinimalApiSecurity()` | Applies security headers and HSTS before anything else writes the response |
| 2 | `UseArkProblemDetailsExceptionHandler()` | Converts unhandled domain exceptions |
| 3 | `UseArkMinimalApiHost(container)` | Validates `X-Forwarded-Prefix`, selects endpoints, builds the caller principal, enforces host-level authorization, and makes the scoped application graph available to handlers |
| 4 | `UseEndpoints(...)` | Maps generated HTTP, gRPC, OpenAPI, and any hand-written endpoints |

```csharp
app.UseArkMinimalApiSecurity();
app.UseArkProblemDetailsExceptionHandler();
app.UseArkMinimalApiHost(container);

app.UseEndpoints(endpoints =>
{
    endpoints.MapArkEndpointsFromAssembly<ApplicationAssemblyMarker>();
    endpoints.MapArkGrpcServicesFromAssembly<ApplicationAssemblyMarker>();
    endpoints.MapOpenApi().AllowAnonymous();
});
```

`UseArkMinimalApiHost` applies routing, authentication, authorization, and
SimpleInjector middleware in that order. Keep it before endpoint mapping so
generated endpoints see the authenticated caller and authorization runs before
they execute. A valid `X-Forwarded-Prefix` is prepended to `PathBase`; malformed
or ambiguous values are rejected with `400` before downstream middleware runs.
The prefix is applied before generated OpenAPI endpoints execute, so prefixed
deployments retain their generated document paths and links.
Set `UseForwardedPrefix` to `false` when the deployment handles this header
outside the application.

`UseArkMinimalApiStartupDiagnostics` is optional. It enables ASP.NET Core startup
error capture and detailed startup diagnostics for smoke tests and development.
Hosts that compose ASP.NET Core directly may omit it and select their own hosting
diagnostic settings.

## Map generated endpoints from a marker type

The framework scans the assembly containing the marker type you provide. The
marker does not need special behavior; it only anchors assembly selection.
The sample uses `RefreshGreetingCommand` because it is guaranteed to live in the
application assembly.

```csharp
endpoints.MapArkEndpointsFromAssembly<RefreshGreetingCommand>();
endpoints.MapArkGrpcServicesFromAssembly<RefreshGreetingCommand>();
ArkGeneratedEndpoints.RegisterArkRebusHandlersFromAssembly<RefreshGreetingCommand>(container);
cfg.Routing(ArkGeneratedEndpoints.ConfigureArkRebusRouting<RefreshGreetingCommand>);
```

Expected result:

- every `[HttpEndpoint]` contract in that assembly becomes an HTTP route;
- every `[GrpcMethod]` contract becomes part of a generated gRPC service;
- every `[RebusMessage]` contract gets generated Rebus glue.

## Configure OpenAPI once per version

The sample applies the same per-document configuration to both `v1` and `v2`:

```csharp
private void ConfigureOpenApi(OpenApiOptions options)
{
    options
        .AddArkTypeConverterValueSchemas()
        .AddArkNodaTimeSchemas()
        .AddArkServerSetProperties()
        .AddArkXmlDocumentation()
        .AddArkOAuthSecurity(openApiSecurity)
        .AddArkPolymorphism<Shape, ShapeKind>("kind", (ShapeKind.Circle, typeof(Circle)));
}
```

That setup ensures the published document matches the public HTTP contract:

- XML comments become descriptions.
- NodaTime values get stable schema shapes.
- `[ServerSet]` fields stay out of request schemas.
- OAuth requirements are visible in Swagger/Scalar.
- registered polymorphic hierarchies show the discriminator users must send.

## Rebus host setup

`SampleComposition.BuildContainer(...)` owns Rebus transport setup. The key
behaviors are:

- one SimpleInjector scope per received message;
- generated handler registration from the application assembly;
- generated routing for every `[RebusMessage(OwnerQueue = ...)]` contract;
- optional protobuf serialization; otherwise JSON serialization;
- fail-fast dead-letter behavior via `ArkRetryStrategy(maxDeliveryAttempts: 1)`.

This lets the same pure handler run under HTTP, gRPC, or Rebus with the same
validators and authorization decorators.

## Copy-from-sample checklist

When building a new host, inspect these files in this order:

1. `ApplicationComposition.cs`
2. `GreetingAuthorizationPolicy.cs`
3. `SampleStartup.cs`
4. `SampleComposition.cs`
5. `test/Ark.MediatorFramework.Sample.Tests/Hooks/SampleTestContext.cs`

If your application must diverge from those patterns, document why: host setup
is part of the framework's public operational behavior.

Architecture rationale: [design.md](../design.md).
