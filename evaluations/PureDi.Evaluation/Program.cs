// Evaluation PoC: Pure.DI source-generated composition
// Verifies the SimpleInjector feature-set Ark.Tools relies upon:
//  1. open-generic registration via TT marker types (compile-time monomorphization)
//  2. OPEN GENERIC DECORATORS via TT markers + tag-chained bindings
//  3. conditional fallback registration (exact IValidator<PingQuery> binding wins over IValidator<TT>)
//  4. runtime mediator dispatch via generated Resolve(Type) over per-handler roots
//  5. scoped lifetime via child composition (Session pattern)
//  6. Func<T> factory resolution
// Compile-time verification: any missing dependency is a BUILD ERROR (SimpleInjector Verify() at compile time).
// Run with: dotnet run   |   AOT: dotnet publish -c Release -p:PublishAot=true

using PureDi.Evaluation;

var composition = new Composition();

// (4) runtime mediator dispatch, same pattern as SimpleInjectorQueryProcessor
var processor = new QueryProcessor(composition);

var r1 = await processor.ExecuteAsync(new PingQuery("hello"), CancellationToken.None);
Console.WriteLine($"Ping => {r1}");
var r2 = await processor.ExecuteAsync(new CountQuery(21), CancellationToken.None);
Console.WriteLine($"Count => {r2}");

Assert(r1 == "AUDITED(VALIDATED(pong:hello))", "open-generic decorator chain on string handler");
Assert(r2 == 42, "open-generic decorator chain on int handler (value-type TResult on AOT)");

// (3) exact-match binding wins over TT fallback
Assert(composition.PingValidator is PingValidator, "specific validator wins over fallback");
Assert(composition.CountValidator is NullValidator<CountQuery>, "fallback open-generic validator");

// (5) scoped lifetime: same instance within a scope, different across scopes
{
    var s1 = new Session(composition);
    var s2 = new Session(composition);
    Assert(ReferenceEquals(s1.Connection, s1.Connection), "scoped lifetime identity within scope");
    Assert(!ReferenceEquals(s1.Connection, s2.Connection), "different instances across scopes");
}

// (6) Func<T> factory
var factory = composition.HandlerFactory;
Assert(factory() is not null, "Func<T> factory resolution");

Console.WriteLine("ALL CHECKS PASSED");
return 0;

static void Assert(bool condition, string what)
{
    if (!condition) throw new InvalidOperationException($"FAILED: {what}");
    Console.WriteLine($"  ok: {what}");
}

namespace PureDi.Evaluation
{
    using Pure.DI;

    // ---- Ark.Tools.Solid-like abstractions ----
    public interface IQuery<TResult> { }

    // AOT-safe replacement of the `dynamic` dispatch used by SimpleInjectorQueryProcessor
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
    public interface IDbConnectionManager { }

    public sealed record PingQuery(string Message) : IQuery<string>;
    public sealed record CountQuery(int Value) : IQuery<int>;

    public sealed class PingHandler : IQueryHandler<PingQuery, string>
    {
        public Task<string> ExecuteAsync(PingQuery query, CancellationToken ctk) => Task.FromResult($"pong:{query.Message}");
    }

    public sealed class CountHandler : IQueryHandler<CountQuery, int>
    {
        public Task<int> ExecuteAsync(CountQuery query, CancellationToken ctk) => Task.FromResult(query.Value * 2);
    }

    public sealed class PingValidator : IValidator<PingQuery>
    {
        public void Validate(PingQuery instance) { if (string.IsNullOrEmpty(instance.Message)) throw new ArgumentException("empty", nameof(instance)); }
    }

    public sealed class NullValidator<T> : IValidator<T> { public void Validate(T instance) { } }

    // (2) open-generic decorators; tag-chained: "base" -> "validated" -> default
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

    public sealed class FakeConnectionManager : IDbConnectionManager { }

    // custom Pure.DI generic markers: TTQuery is constrained to IQuery<TT>, mirroring the handler constraint
    [GenericTypeArgument]
    internal interface TTQuery : IQuery<TT> { }

    public partial class Composition
    {
        private static void Setup() =>
            DI.Setup(nameof(Composition))
                // (1) per-handler "base" bindings (what compile-time batch registration would emit)
                .Bind<IQueryHandler<PingQuery, string>>("base").To<PingHandler>()
                .Bind<IQueryHandler<CountQuery, int>>("base").To<CountHandler>()

                // (2) open-generic decorator chain via TT markers, applied to EVERY handler
                .Bind<IQueryHandler<TTQuery, TT>>("validated").To<ValidationDecorator<TTQuery, TT>>()
                .Bind<IQueryHandler<TTQuery, TT>>().To<AuditDecorator<TTQuery, TT>>()

                // (3) fallback validator via TT + exact-match override
                .Bind<IValidator<TT>>().To<NullValidator<TT>>()
                .Bind<IValidator<PingQuery>>().To<PingValidator>()

                // (5) scoped
                .Bind<IDbConnectionManager>().As(Lifetime.Scoped).To<FakeConnectionManager>()

                // roots: private roots are resolvable via Resolve(Type) for mediator dispatch
                .Root<IQueryHandler<PingQuery, string>>()
                .Root<IQueryHandler<CountQuery, int>>()
                .Root<IValidator<PingQuery>>("PingValidator")
                .Root<IValidator<CountQuery>>("CountValidator")
                .Root<IDbConnectionManager>("Connection")
                // (6) Func factory root
                .Root<Func<IQueryHandler<PingQuery, string>>>("HandlerFactory");
    }

    // (5) scope = child composition
    public sealed class Session : Composition
    {
        public Session(Composition parent) : base(parent) { }
    }

    // (4) SimpleInjectorQueryProcessor equivalent using generated Resolve(Type)
    public sealed class QueryProcessor
    {
        private readonly Composition _composition;
        public QueryProcessor(Composition composition) { _composition = composition; }

        public async Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
            var handler = (IQueryHandlerBase<TResult>)_composition.Resolve(handlerType);
            return await handler.ExecuteAsync(query, ctk);
        }
    }
}
