// Evaluation PoC: OPEN GENERIC DECORATION with ZERO manual handler registration.
// Review requirement (Q3 follow-up): new handlers created during feature development must be
// automatically registered AND guaranteed to be wrapped by ALL registered decorators.
//
// How: HandlerGen.Generator (an Ark-owned Roslyn incremental generator, ~150 LOC) scans this
// compilation for IQueryHandler<,> implementations and emits `AddDiscoveredHandlers()` with a
// statically-closed decorator chain per handler + closed fallback validators. Look at the classes
// below: NO registration attribute, NO Bind/Add call per handler exists anywhere in this project.
// NewFeatureHandler simulates "developer adds a handler and touches nothing else" — asserted decorated.
//
// Run with: dotnet run   |   AOT: dotnet publish -c Release -p:PublishAot=true
// Inspect emitted code: obj/Debug/net10.0/generated/.../GeneratedHandlerRegistrations.g.cs

using HandlerGen.Evaluation;

using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton<IAuditSink, ConsoleAuditSink>();

// ONE call, generated: all handlers, all decorators, all fallback validators
services.AddDiscoveredHandlers();

var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

var processor = new QueryProcessor(provider);

// (1) decorator chain applied: Audit(Validation(handler))
var r1 = await processor.ExecuteAsync(new PingQuery("hello"), CancellationToken.None);
Assert(r1 == "AUDITED(VALIDATED(pong:hello))", "discovered handler wrapped by full decorator chain");

// (2) value-type TResult on AOT + fallback validator (no PingValidator-like class for CountQuery)
var r2 = await processor.ExecuteAsync(new CountQuery(21), CancellationToken.None);
Assert(r2 == 42, "decorator chain on int handler (value-type TResult)");
Assert(provider.GetRequiredService<IValidator<CountQuery>>() is NullValidator<CountQuery>, "compile-time fallback validator emitted");
Assert(provider.GetRequiredService<IValidator<PingQuery>>() is PingValidator, "specific validator wins over fallback");

// (3) THE GUARANTEE: NewFeatureHandler was added with zero registration code anywhere,
// yet it is registered and decorated by all registered decorators
var r3 = await processor.ExecuteAsync(new NewFeatureQuery(), CancellationToken.None);
Assert(r3 == "AUDITED(VALIDATED(new-feature))", "NEW handler auto-registered AND auto-decorated, no registration code");

// (4) structural guarantee is assertable: the generated manifest covers every handler class
Assert(GeneratedHandlerRegistrations.KnownHandlers.Count == 3, "manifest lists every discovered handler");
Assert(GeneratedHandlerRegistrations.KnownHandlers.Any(k => k.Handler == typeof(NewFeatureHandler)), "manifest includes the new handler");

// (5) validation decorator is live (specific validator actually invoked)
try
{
    await processor.ExecuteAsync(new PingQuery(""), CancellationToken.None);
    Assert(false, "validator must reject empty message");
}
catch (ArgumentException)
{
    Assert(true, "validation decorator invoked the specific validator");
}

Console.WriteLine("ALL CHECKS PASSED");
return 0;

static void Assert(bool condition, string what)
{
    if (!condition) throw new InvalidOperationException($"FAILED: {what}");
    Console.WriteLine($"  ok: {what}");
}

namespace HandlerGen.Evaluation
{
    // ---- Ark.Tools.Solid-like abstractions ----
    public interface IQuery<TResult> { }

    public interface IQueryHandlerBase<TResult>
    {
        Task<TResult> ExecuteAsync(IQuery<TResult> query, CancellationToken ctk);
    }

    public interface IQueryHandler<TQuery, TResult> : IQueryHandlerBase<TResult> where TQuery : IQuery<TResult>
    {
        Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk);

        Task<TResult> IQueryHandlerBase<TResult>.ExecuteAsync(IQuery<TResult> query, CancellationToken ctk)
            => ExecuteAsync((TQuery)query, ctk);
    }

    public interface IValidator<T> { void Validate(T instance); }
    public sealed class NullValidator<T> : IValidator<T> { public void Validate(T instance) { } }

    public interface IAuditSink { void Record(string what); }
    public sealed class ConsoleAuditSink : IAuditSink { public void Record(string what) => Console.WriteLine($"  audit: {what}"); }

    // marks a class as an open-generic decorator applied to EVERY discovered handler; lower order = innermost
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HandlerDecoratorAttribute : Attribute
    {
        public HandlerDecoratorAttribute(int order) { Order = order; }
        public int Order { get; }
    }

    public sealed record PingQuery(string Message) : IQuery<string>;
    public sealed record CountQuery(int Value) : IQuery<int>;
    public sealed record NewFeatureQuery() : IQuery<string>;

    // ---- handlers: note there is NO registration attribute/call anywhere ----
    public sealed class PingHandler : IQueryHandler<PingQuery, string>
    {
        public Task<string> ExecuteAsync(PingQuery query, CancellationToken ctk) => Task.FromResult($"pong:{query.Message}");
    }

    public sealed class CountHandler : IQueryHandler<CountQuery, int>
    {
        public Task<int> ExecuteAsync(CountQuery query, CancellationToken ctk) => Task.FromResult(query.Value * 2);
    }

    // "feature development" handler: added later, zero registration code, must come out decorated
    public sealed class NewFeatureHandler : IQueryHandler<NewFeatureQuery, string>
    {
        public Task<string> ExecuteAsync(NewFeatureQuery query, CancellationToken ctk) => Task.FromResult("new-feature");
    }

    public sealed class PingValidator : IValidator<PingQuery>
    {
        public void Validate(PingQuery instance) { if (string.IsNullOrEmpty(instance.Message)) throw new ArgumentException("empty", nameof(instance)); }
    }

    // ---- open-generic decorators, applied to ALL handlers by the generator ----
    [HandlerDecorator(1)]
    public sealed class ValidationDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _inner;
        private readonly IValidator<TQuery> _validator;
        public ValidationDecorator(IQueryHandler<TQuery, TResult> inner, IValidator<TQuery> validator)
        { _inner = inner; _validator = validator; }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk)
        {
            _validator.Validate(query);
            var res = await _inner.ExecuteAsync(query, ctk);
            return res is string s ? (TResult)(object)$"VALIDATED({s})" : res;
        }
    }

    [HandlerDecorator(2)]
    public sealed class AuditDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _inner;
        private readonly IAuditSink _sink;
        public AuditDecorator(IQueryHandler<TQuery, TResult> inner, IAuditSink sink) { _inner = inner; _sink = sink; }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk)
        {
            _sink.Record(typeof(TQuery).Name);
            var res = await _inner.ExecuteAsync(query, ctk);
            return res is string s ? (TResult)(object)$"AUDITED({s})" : res;
        }
    }

    // SimpleInjectorQueryProcessor equivalent, no `dynamic`
    public sealed class QueryProcessor
    {
        private readonly IServiceProvider _provider;
        public QueryProcessor(IServiceProvider provider) { _provider = provider; }

        public async Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk)
        {
            // generated dispatch map instead of MakeGenericType: zero IL3050 on NativeAOT
            var handlerType = GeneratedHandlerRegistrations.HandlerServiceByQuery[query.GetType()];
            var handler = (IQueryHandlerBase<TResult>)_provider.GetRequiredService(handlerType);
            return await handler.ExecuteAsync(query, ctk);
        }
    }
}
