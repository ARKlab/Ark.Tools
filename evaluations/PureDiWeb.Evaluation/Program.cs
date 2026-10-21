// Evaluation PoC: Pure.DI + ASP.NET Core cross-wiring (F10)
// Replicates what Ark.Tools.AspNetCore does with SimpleInjector's AddSimpleInjector/AddAspNetCore:
//  1. controller activation from the Pure.DI composition (Roots<ControllerBase>() + AddControllersAsServices) — CoreCLR only, see below
//  2. minimal-API endpoint consuming the DECORATED open-generic query handler pipeline from the composition
//  3. health check resolving a service from the composition (Ark.Tools.AspNetCore.HealthChecks pattern)
//  4. two-way resolution: composition classes consume framework services (ILogger<T>) from IServiceProvider
// Self-check: the app starts Kestrel on a loopback port, calls itself over HTTP and asserts the responses.
//
// EMPIRICAL FINDING (container-independent): MVC does NOT survive trimming, full stop.
//  * AddControllers() is [RequiresUnreferencedCode] "MVC does not currently support trimming or native AOT";
//  * controller discovery relies on Assembly.DefinedTypes (trimmed -> 404);
//  * even with <TrimmerRootAssembly> rooting the app assembly, MapControllers() throws at startup:
//    NotSupportedException "IsConvertibleType is not initialized when Microsoft.AspNetCore.Mvc.ApiExplorer.IsEnhancedModelMetadataSupported is false"
//    (ModelMetadata feature-switched off under TrimMode=full, .NET 10).
// The trimmed web story is Minimal APIs; this is an Ark.Tools.AspNetCore startup concern, not a DI-container concern.
// Controller activation is therefore asserted on CoreCLR only (pass --with-mvc), minimal API + health checks on both.
// Run with: dotnet run -- --with-mvc   |   trimmed: dotnet publish -c Release -p:PublishTrimmed=true -p:TrimMode=full --self-contained -r linux-x64

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using PureDiWeb.Evaluation;

var withMvc = args.Contains("--with-mvc"); // MVC cannot run trimmed (see header) - CoreCLR-only assertion

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");

var composition = new Composition();
builder.Host.UseServiceProviderFactory(composition);

if (withMvc)
    builder.Services.AddControllers().AddControllersAsServices();
builder.Services.AddHealthChecks().AddCheck<CompositionHealthCheck>("composition");

var app = builder.Build();
if (withMvc)
    app.MapControllers();
// minimal API endpoint resolving the composition-managed decorated pipeline (the trimmed-deployment path)
app.MapGet("/mping/{message}", async (string message, HttpContext ctx) =>
{
    var handler = ctx.RequestServices.GetRequiredService<IQueryHandler<PingQuery, string>>();
    return await handler.ExecuteAsync(new PingQuery(message), ctx.RequestAborted);
});
app.MapHealthChecks("/health");

await app.StartAsync();

// ---- self-check over real HTTP ----
var baseUrl = app.Urls.First();
using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

if (withMvc)
{
    var ping = await http.GetStringAsync("/ping/hello");
    Assert(ping == "AUDITED(VALIDATED(pong:hello))", "controller activated from composition with full decorator chain");
}

var mping = await http.GetStringAsync("/mping/hello");
Assert(mping == "AUDITED(VALIDATED(pong:hello))", "minimal-API endpoint resolving decorated pipeline from composition");

var health = await http.GetAsync("/health");
Assert(health.IsSuccessStatusCode, "health check resolving from composition");

Assert(CompositionHealthCheck.Ran, "health check actually executed");
Assert(PingHandler.LoggerWasInjected, "framework ILogger<T> cross-wired into composition-managed handler");

Console.WriteLine("ALL CHECKS PASSED");
await app.StopAsync();
return 0;

static void Assert(bool condition, string what)
{
    if (!condition) throw new InvalidOperationException($"FAILED: {what}");
    Console.WriteLine($"  ok: {what}");
}

namespace PureDiWeb.Evaluation
{
    using Pure.DI;
    using Pure.DI.MS;

    // ---- Ark.Tools.Solid-like abstractions (same as PureDi.Evaluation) ----
    public interface IQuery<TResult> { }

    public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk);
    }

    public interface IValidator<T> { void Validate(T instance); }

    public sealed record PingQuery(string Message) : IQuery<string>;

    public sealed class PingHandler : IQueryHandler<PingQuery, string>
    {
        public static bool LoggerWasInjected;

        // ILogger<T> is a FRAMEWORK service: Pure.DI.MS resolves it from IServiceProvider (two-way cross-wiring)
        public PingHandler(ILogger<PingHandler> logger) { LoggerWasInjected = logger is not null; }

        public Task<string> ExecuteAsync(PingQuery query, CancellationToken ctk) => Task.FromResult($"pong:{query.Message}");
    }

    public sealed class NullValidator<T> : IValidator<T> { public void Validate(T instance) { } }

    public sealed class ValidationDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _inner;
        private readonly IValidator<TQuery> _validator;
        public ValidationDecorator([Tag("base")] IQueryHandler<TQuery, TResult> inner, IValidator<TQuery> validator)
        { _inner = inner; _validator = validator; }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk)
        {
            _validator.Validate(query);
            var res = await _inner.ExecuteAsync(query, ctk);
            return res is string s ? (TResult)(object)$"VALIDATED({s})" : res;
        }
    }

    public sealed class AuditDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _inner;
        public AuditDecorator([Tag("validated")] IQueryHandler<TQuery, TResult> inner) { _inner = inner; }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk)
        {
            var res = await _inner.ExecuteAsync(query, ctk);
            return res is string s ? (TResult)(object)$"AUDITED({s})" : res;
        }
    }

    // controller resolved FROM the Pure.DI composition, consuming the decorated pipeline
    [ApiController]
    public sealed class PingController : ControllerBase
    {
        private readonly IQueryHandler<PingQuery, string> _handler;
        public PingController(IQueryHandler<PingQuery, string> handler) { _handler = handler; }

        [HttpGet("/ping/{message}")]
        public async Task<string> Get(string message) => await _handler.ExecuteAsync(new PingQuery(message), HttpContext.RequestAborted);
    }

    // health check resolving a composition service, like Ark.Tools.AspNetCore.HealthChecks adapters
    public sealed class CompositionHealthCheck : IHealthCheck
    {
        public static bool Ran;
        private readonly IValidator<PingQuery> _validator;
        public CompositionHealthCheck(IValidator<PingQuery> validator) { _validator = validator; }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            Ran = true;
            return Task.FromResult(_validator is not null ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
        }
    }

    [GenericTypeArgument]
    internal interface TTQuery : IQuery<TT> { }

    public partial class Composition : ServiceProviderFactory<Composition>
    {
        [System.Diagnostics.Conditional("DI")]
        private static void Setup() =>
            DI.Setup(nameof(Composition))

                .Bind<IQueryHandler<PingQuery, string>>("base").To<PingHandler>()
                .Bind<IQueryHandler<TTQuery, TT>>("validated").To<ValidationDecorator<TTQuery, TT>>()
                .Bind<IQueryHandler<TTQuery, TT>>().To<AuditDecorator<TTQuery, TT>>()
                .Bind<IValidator<TT>>().To<NullValidator<TT>>()

                // roots resolvable via IServiceProvider: controllers + health check + minimal-API handler
                .Roots<ControllerBase>()
                .Root<CompositionHealthCheck>()
                .Root<IQueryHandler<PingQuery, string>>()
                .Root<IValidator<PingQuery>>();
    }
}
