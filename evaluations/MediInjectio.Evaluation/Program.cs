// Evaluation PoC: Microsoft.Extensions.DependencyInjection (MEDI) + Injectio source generator
// Verifies the SimpleInjector feature-set Ark.Tools relies upon:
//  1. compile-time batch registration of handlers (Injectio attributes ~ GetTypesToRegister)
//  2. OPEN GENERIC DECORATORS applied to handlers (RegisterDecorator(typeof(IQueryHandler<,>), ...))
//  3. conditional fallback registration (RegisterConditional NullValidator<> when no specific validator)
//  4. runtime mediator dispatch: GetRequiredService(typeof(IQueryHandler<,>).MakeGenericType(...))
//     using an AOT-safe non-generic bridge instead of `dynamic` (dynamic requires dynamic code)
//  5. scoped lifetimes + ValidateOnBuild/ValidateScopes (SimpleInjector Verify()-lite)
//  6. collection resolution (GetServices<T> ~ GetAllInstances)
// Run with: dotnet run   |   AOT: dotnet publish -c Release -p:PublishAot=true

using MediInjectio.Evaluation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// (7) NativeAOT FINDING: Injectio's open-generic decoration closes decorator types at runtime
// (ActivatorUtilities). On NativeAOT the closed instantiations (and their ctor native code) don't
// exist unless something references them statically. GeneratedAotRoots simulates what a small
// Ark-owned source generator would emit: one closed root per (handler x decorator) pair.
// Without this call the AOT-published binary crashes with
// "A suitable constructor for type 'ValidationDecorator`2[PingQuery,String]' could not be located".
GeneratedAotRoots.Root();

var services = new ServiceCollection();

// (1) Injectio generated extension: registers all [Register*] annotated types of this assembly
services.AddMediInjectioEvaluation();

// (3) conditional fallback: open-generic NullValidator<> only used when no closed validator exists.
services.TryAdd(ServiceDescriptor.Singleton(typeof(IValidator<>), typeof(NullValidator<>)));

var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    // (5) SimpleInjector Verify()-lite: builds every registration eagerly and validates scopes
    ValidateOnBuild = true,
    ValidateScopes = true,
});

using (var scope = provider.CreateScope())
{
    // (4) runtime mediator dispatch, same pattern as SimpleInjectorQueryProcessor
    var scoped = new QueryProcessor(scope.ServiceProvider);

    // PingQuery has a real validator, decorator chain must be Audit(Validation(handler))
    var r1 = await scoped.ExecuteAsync(new PingQuery("hello"), CancellationToken.None);
    Console.WriteLine($"Ping => {r1}");
    // CountQuery has no validator -> fallback NullValidator<CountQuery> must kick in
    var r2 = await scoped.ExecuteAsync(new CountQuery(21), CancellationToken.None);
    Console.WriteLine($"Count => {r2}");

    // assertions = the actual proof
    Assert(r1 == "AUDITED(VALIDATED(pong:hello))", "open-generic decorator chain on string handler");
    Assert(r2 == 42, "open-generic decorator chain on int handler (value-type TResult on AOT)");
    Assert(scope.ServiceProvider.GetRequiredService<IValidator<PingQuery>>() is PingValidator, "specific validator wins over fallback");
    Assert(scope.ServiceProvider.GetRequiredService<IValidator<CountQuery>>() is NullValidator<CountQuery>, "fallback open-generic validator");

    // (6) GetAllInstances equivalent (Rebus IHandleMessages<T> pattern)
    var handlers = scope.ServiceProvider.GetServices<IHandleMessages<string>>().ToList();
    Assert(handlers.Count == 2, "collection resolution of message handlers");

    // scoped lifetime identity within scope
    var c1 = scope.ServiceProvider.GetRequiredService<IDbConnectionManager>();
    var c2 = scope.ServiceProvider.GetRequiredService<IDbConnectionManager>();
    Assert(ReferenceEquals(c1, c2), "scoped lifetime identity");
}

// Func<T> factory: NOT supported natively by MEDI (SimpleInjector AllowResolvingFuncFactories has no equivalent)
Assert(provider.GetService<Func<IDbConnectionManager>>() is null, "Func<T> auto-factory NOT available in MEDI (known gap)");

Console.WriteLine("ALL CHECKS PASSED");
return 0;

static void Assert(bool condition, string what)
{
    if (!condition) throw new InvalidOperationException($"FAILED: {what}");
    Console.WriteLine($"  ok: {what}");
}

namespace MediInjectio.Evaluation
{
    using Injectio.Attributes;

    using System.Diagnostics.CodeAnalysis;

    using Microsoft.Extensions.DependencyInjection;

    // ---- Ark.Tools.Solid-like abstractions ----
    public interface IQuery<TResult> { }

    // AOT-safe replacement of the `dynamic` dispatch used by SimpleInjectorQueryProcessor:
    // non-generic-in-TQuery bridge implemented via default interface method.
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
    public interface IHandleMessages<TMessage> { Task Handle(TMessage message); }
    public interface IDbConnectionManager { }

    public sealed record PingQuery(string Message) : IQuery<string>;
    public sealed record CountQuery(int Value) : IQuery<int>;

    // (1) compile-time batch registration by Injectio (closed interface registrations)
    [RegisterSingleton(Registration = RegistrationStrategy.ImplementedInterfaces)]
    public sealed class PingHandler : IQueryHandler<PingQuery, string>
    {
        public Task<string> ExecuteAsync(PingQuery query, CancellationToken ctk) => Task.FromResult($"pong:{query.Message}");
    }

    [RegisterSingleton(Registration = RegistrationStrategy.ImplementedInterfaces)]
    public sealed class CountHandler : IQueryHandler<CountQuery, int>
    {
        public Task<int> ExecuteAsync(CountQuery query, CancellationToken ctk) => Task.FromResult(query.Value * 2);
    }

    [RegisterSingleton(Registration = RegistrationStrategy.ImplementedInterfaces)]
    public sealed class PingValidator : IValidator<PingQuery>
    {
        public void Validate(PingQuery instance) { if (string.IsNullOrEmpty(instance.Message)) throw new ArgumentException("empty"); }
    }

    public sealed class NullValidator<T> : IValidator<T> { public void Validate(T instance) { } }

    // (2) open-generic decorators over the closed handler registrations, innermost first
    [RegisterDecorator(ServiceType = typeof(IQueryHandler<,>), Order = 1)]
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

    [RegisterDecorator(ServiceType = typeof(IQueryHandler<,>), Order = 2)]
    public sealed class AuditDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _inner;
        public AuditDecorator(IQueryHandler<TQuery, TResult> inner) { _inner = inner; }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk)
        {
            var res = await _inner.ExecuteAsync(query, ctk);
            return res is string s ? (TResult)(object)$"AUDITED({s})" : res;
        }
    }

    // (6) multiple handlers for the same message (Rebus pattern)
    [RegisterSingleton(Registration = RegistrationStrategy.ImplementedInterfaces, Duplicate = DuplicateStrategy.Append)]
    public sealed class StringHandlerA : IHandleMessages<string> { public Task Handle(string message) => Task.CompletedTask; }
    [RegisterSingleton(Registration = RegistrationStrategy.ImplementedInterfaces, Duplicate = DuplicateStrategy.Append)]
    public sealed class StringHandlerB : IHandleMessages<string> { public Task Handle(string message) => Task.CompletedTask; }

    [RegisterScoped(Registration = RegistrationStrategy.ImplementedInterfaces)]
    public sealed class FakeConnectionManager : IDbConnectionManager { }

    // (7) what an Ark source generator would emit to make runtime-closed decorators AOT-safe:
    // roots the reflection metadata (public ctors) of the closed generic instantiations,
    // since Injectio closes decorators at runtime via ActivatorUtilities (reflection).
    public static class GeneratedAotRoots
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(ValidationDecorator<PingQuery, string>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(AuditDecorator<PingQuery, string>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(ValidationDecorator<CountQuery, int>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(AuditDecorator<CountQuery, int>))]
        public static void Root()
        {
            // value-type generic args need the actual native instantiation compiled by ILC
            // (reference-type args share the __Canon instantiation, metadata rooting suffices)
            _ = new ValidationDecorator<CountQuery, int>(null!, null!);
            _ = new AuditDecorator<CountQuery, int>(null!);
        }
    }

    // (4) SimpleInjectorQueryProcessor equivalent on MEDI, without `dynamic`
    public sealed class QueryProcessor
    {
        private readonly IServiceProvider _provider;
        public QueryProcessor(IServiceProvider provider) { _provider = provider; }

        public async Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk)
        {
            // MakeGenericType over an instantiation that exists in compiled code is AOT-safe
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
            var handler = (IQueryHandlerBase<TResult>)_provider.GetRequiredService(handlerType);
            return await handler.ExecuteAsync(query, ctk);
        }
    }
}
