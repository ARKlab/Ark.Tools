// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.Tools.Solid.SimpleInjector.Tests;

[TestClass]
public sealed class ServiceCollectionProcessorBridgeTests
{
    private static readonly string[] _expectedEvents =
    [
        "request-decorator",
        "request-handler",
        "query-decorator",
        "query-handler",
        "command-decorator",
        "command-handler",
    ];

    [TestMethod]
    public async Task AddArkSolidProcessors_resolves_processors_backed_by_the_supplied_container()
    {
        await using var container = _createContainer();
        await using var provider = _createProvider(container);
        var trace = container.GetInstance<Trace>();

        var requestProcessor = provider.GetRequiredService<IRequestProcessor>();
        var queryProcessor = provider.GetRequiredService<IQueryProcessor>();
        var commandProcessor = provider.GetRequiredService<ICommandProcessor>();

        var requestResult = await requestProcessor.ExecuteAsync(new TestRequest(1)).ConfigureAwait(false);
        var queryResult = await queryProcessor.ExecuteAsync(new TestQuery(2)).ConfigureAwait(false);
        await commandProcessor.ExecuteAsync(new TestCommand(3)).ConfigureAwait(false);

        requestResult.Should().Be(1);
        queryResult.Should().Be(2);
        trace.Events.Should().Equal(_expectedEvents);
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_creates_a_scope_when_none_is_active()
    {
        await using var container = _createContainer();
        await using var provider = _createProvider(container);
        var requestProcessor = provider.GetRequiredService<IRequestProcessor>();

        var first = await requestProcessor.ExecuteAsync(new ScopeRequest()).ConfigureAwait(false);
        var second = await requestProcessor.ExecuteAsync(new ScopeRequest()).ConfigureAwait(false);

        first.Should().NotBe(second);
        container.Options.DefaultScopedLifestyle!.GetCurrentScope(container).Should().BeNull();
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_uses_the_configured_scoped_lifestyle()
    {
        await using var container = _createContainer(scopedLifestyle: new ThreadScopedLifestyle());
        await using var provider = _createProvider(container);
        var requestProcessor = provider.GetRequiredService<IRequestProcessor>();

        var first = await requestProcessor.ExecuteAsync(new ScopeRequest()).ConfigureAwait(false);
        var second = await requestProcessor.ExecuteAsync(new ScopeRequest()).ConfigureAwait(false);

        first.Should().NotBe(second);
        container.Options.DefaultScopedLifestyle!.GetCurrentScope(container).Should().BeNull();
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_reuses_the_active_scope()
    {
        await using var container = _createContainer();
        await using var provider = _createProvider(container);
        var requestProcessor = provider.GetRequiredService<IRequestProcessor>();

        await using (AsyncScopedLifestyle.BeginScope(container).ConfigureAwait(false))
        {
            var currentProbe = container.GetInstance<ScopeProbe>();

            var observed = await requestProcessor.ExecuteAsync(new ScopeRequest()).ConfigureAwait(false);

            observed.Should().Be(currentProbe.InstanceId);
        }
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_propagates_cancellation_and_exceptions()
    {
        await using var container = _createContainer();
        await using var provider = _createProvider(container);
        var queryProcessor = provider.GetRequiredService<IQueryProcessor>();
        var requestProcessor = provider.GetRequiredService<IRequestProcessor>();
        var cancellationToken = new CancellationToken(canceled: true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await queryProcessor.ExecuteAsync(new CancellableQuery(), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await requestProcessor.ExecuteAsync(new FailingRequest(), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_reuses_the_active_scope_for_nested_processor_calls()
    {
        await using var container = _createContainer();
        await using var provider = _createProvider(container);
        var requestProcessor = provider.GetRequiredService<IRequestProcessor>();

        var result = await requestProcessor.ExecuteAsync(new NestedRequest()).ConfigureAwait(false);

        result.OuterScopeId.Should().Be(result.InnerScopeId);
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_preserves_existing_microsoft_dependency_injection_registrations()
    {
        await using var container = _createContainer();
        var existingRequestProcessor = new SimpleInjectorRequestProcessor(container);
        var existingQueryProcessor = new SimpleInjectorQueryProcessor(container);
        var existingCommandProcessor = new SimpleInjectorCommandProcessor(container);
        var services = new ServiceCollection();
        services.AddSingleton<IRequestProcessor>(existingRequestProcessor);
        services.AddSingleton<IQueryProcessor>(existingQueryProcessor);
        services.AddSingleton<ICommandProcessor>(existingCommandProcessor);

        await using var provider = _createProvider(container, services);

        provider.GetRequiredService<IRequestProcessor>().Should().BeSameAs(existingRequestProcessor);
        provider.GetRequiredService<IQueryProcessor>().Should().BeSameAs(existingQueryProcessor);
        provider.GetRequiredService<ICommandProcessor>().Should().BeSameAs(existingCommandProcessor);
        provider.GetServices<IRequestProcessor>().Should().ContainSingle().Which.Should().BeSameAs(existingRequestProcessor);
        provider.GetServices<IQueryProcessor>().Should().ContainSingle().Which.Should().BeSameAs(existingQueryProcessor);
        provider.GetServices<ICommandProcessor>().Should().ContainSingle().Which.Should().BeSameAs(existingCommandProcessor);
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_is_idempotent_for_repeated_helper_calls()
    {
        await using var container = _createContainer();
        var services = new ServiceCollection();

        services.AddArkSolidProcessors(container);
        services.AddArkSolidProcessors(container);

        await using var provider = _createProvider(container, services);

        provider.GetServices<IRequestProcessor>().Should().ContainSingle();
        provider.GetServices<IQueryProcessor>().Should().ContainSingle();
        provider.GetServices<ICommandProcessor>().Should().ContainSingle();
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_exposes_simpleinjector_handler_registrations_to_generated_host_checks()
    {
        await using var container = _createContainer();
        await using var provider = _createProvider(container);
        var verifier = provider.GetRequiredService<IMediatorHandlerRegistrationVerifier>();

        verifier.IsRegistered(typeof(IRequestHandler<TestRequest, int>)).Should().BeTrue();
        verifier.IsRegistered(typeof(IRequestHandler<UnregisteredRequest, string>)).Should().BeFalse();
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_allows_late_simpleinjector_processor_registrations()
    {
        await using var container = _createEmptyContainer();
        var services = new ServiceCollection();

        services.AddArkSolidProcessors(container);
        _registerDefaultProcessorsAndHandlers(container);

        await using var provider = _buildProvider(container, services);
        var trace = container.GetInstance<Trace>();
        var requestProcessor = provider.GetRequiredService<IRequestProcessor>();
        var queryProcessor = provider.GetRequiredService<IQueryProcessor>();
        var commandProcessor = provider.GetRequiredService<ICommandProcessor>();

        var requestResult = await requestProcessor.ExecuteAsync(new TestRequest(10)).ConfigureAwait(false);
        var queryResult = await queryProcessor.ExecuteAsync(new TestQuery(20)).ConfigureAwait(false);
        await commandProcessor.ExecuteAsync(new TestCommand(30)).ConfigureAwait(false);

        requestResult.Should().Be(10);
        queryResult.Should().Be(20);
        trace.Events.Should().Equal(_expectedEvents);
    }

    [TestMethod]
    public async Task AddArkSolidProcessors_resolves_scoped_simpleinjector_request_processors_inside_the_execution_scope()
    {
        await using var container = _createContainer(static container =>
        {
            container.Register<IRequestProcessor, ScopedTrackingRequestProcessor>(Lifestyle.Scoped);
        });
        await using var provider = _createProvider(container);
        var requestProcessor = provider.GetRequiredService<IRequestProcessor>();

        var first = await requestProcessor.ExecuteAsync(new ScopedProcessorRequest()).ConfigureAwait(false);
        var second = await requestProcessor.ExecuteAsync(new ScopedProcessorRequest()).ConfigureAwait(false);

        first.ProcessorInstanceId.Should().NotBe(second.ProcessorInstanceId);
        first.ScopeInstanceId.Should().NotBe(second.ScopeInstanceId);

        await using (AsyncScopedLifestyle.BeginScope(container).ConfigureAwait(false))
        {
            var inScopeFirst = await requestProcessor.ExecuteAsync(new ScopedProcessorRequest()).ConfigureAwait(false);
            var inScopeSecond = await requestProcessor.ExecuteAsync(new ScopedProcessorRequest()).ConfigureAwait(false);

            inScopeFirst.ProcessorInstanceId.Should().Be(inScopeSecond.ProcessorInstanceId);
            inScopeFirst.ScopeInstanceId.Should().Be(inScopeSecond.ScopeInstanceId);
        }
    }

    private static ServiceProvider _createProvider(Container container, ServiceCollection? services = null)
    {
        services ??= new ServiceCollection();
        services.AddArkSolidProcessors(container);
        return _buildProvider(container, services);
    }

    private static ServiceProvider _buildProvider(Container container, ServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        container.RegisterInstance<IServiceProvider>(provider);
        container.Verify();
        return provider;
    }

    private static Container _createContainer(
        Action<Container>? registerRequestProcessor = null,
        ScopedLifestyle? scopedLifestyle = null)
    {
        var container = _createEmptyContainer(scopedLifestyle);
        _registerDefaultProcessorsAndHandlers(container, registerRequestProcessor);
        return container;
    }

    private static Container _createEmptyContainer(ScopedLifestyle? scopedLifestyle = null)
    {
        return new Container
        {
            Options =
            {
                DefaultScopedLifestyle = scopedLifestyle ?? new AsyncScopedLifestyle(),
            },
        };
    }

    private static void _registerDefaultProcessorsAndHandlers(Container container, Action<Container>? registerRequestProcessor = null)
    {
        if (registerRequestProcessor is null)
        {
            container.RegisterSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();
        }
        else
        {
            registerRequestProcessor(container);
        }

        container.RegisterSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
        container.RegisterSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();
        container.RegisterInstance(new Trace());
        container.Register<ScopeProbe>(Lifestyle.Scoped);
        container.Register<IRequestHandler<TestRequest, int>, TestRequestHandler>();
        container.Register<IRequestHandler<ScopeRequest, Guid>, ScopeRequestHandler>();
        container.Register<IRequestHandler<FailingRequest, int>, FailingRequestHandler>();
        container.Register<IRequestHandler<NestedRequest, NestedScopeResult>, NestedRequestHandler>();
        container.Register<IRequestHandler<InnerNestedRequest, Guid>, InnerNestedRequestHandler>();
        container.Register<IQueryHandler<TestQuery, int>, TestQueryHandler>();
        container.Register<IQueryHandler<CancellableQuery, int>, CancellableQueryHandler>();
        container.Register<ICommandHandler<TestCommand>, TestCommandHandler>();
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestDecorator<,>));
        container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(QueryDecorator<,>));
        container.RegisterDecorator(typeof(ICommandHandler<>), typeof(CommandDecorator<>));
    }

    private sealed class Trace
    {
        public List<string> Events { get; } = [];
    }

    private sealed class ScopeProbe
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    private sealed record NestedScopeResult(Guid OuterScopeId, Guid InnerScopeId);
    private sealed record TestRequest(int Value) : IRequest<TestRequest, int>;
    private sealed record ScopeRequest : IRequest<ScopeRequest, Guid>;
    private sealed record FailingRequest : IRequest<FailingRequest, int>;
    private sealed record UnregisteredRequest : IRequest<UnregisteredRequest, string>;
    private sealed record NestedRequest : IRequest<NestedRequest, NestedScopeResult>;
    private sealed record InnerNestedRequest : IRequest<InnerNestedRequest, Guid>;
    private sealed record ScopedProcessorRequest : IRequest<ScopedProcessorRequest, ScopedProcessorObservation>;
    private sealed record ScopedProcessorObservation(Guid ProcessorInstanceId, Guid ScopeInstanceId);
    private sealed record TestQuery(int Value) : IQuery<TestQuery, int>;
    private sealed record CancellableQuery : IQuery<CancellableQuery, int>;
    private sealed record TestCommand(int Value) : ICommand<TestCommand>;

    private sealed class TestRequestHandler(Trace trace) : IRequestHandler<TestRequest, int>
    {
        public async Task<int> ExecuteAsync(TestRequest request, CancellationToken ctk = default)
        {
            trace.Events.Add("request-handler");
            return await Task.FromResult(request.Value).ConfigureAwait(false);
        }
    }

    private sealed class ScopeRequestHandler(ScopeProbe scopeProbe) : IRequestHandler<ScopeRequest, Guid>
    {
        public async Task<Guid> ExecuteAsync(ScopeRequest request, CancellationToken ctk = default)
        {
            return await Task.FromResult(scopeProbe.InstanceId).ConfigureAwait(false);
        }
    }

    private sealed class FailingRequestHandler : IRequestHandler<FailingRequest, int>
    {
        public async Task<int> ExecuteAsync(FailingRequest request, CancellationToken ctk = default)
        {
            return await Task.FromException<int>(new InvalidOperationException()).ConfigureAwait(false);
        }
    }

    private sealed class NestedRequestHandler(IServiceProvider serviceProvider, ScopeProbe scopeProbe) : IRequestHandler<NestedRequest, NestedScopeResult>
    {
        public async Task<NestedScopeResult> ExecuteAsync(NestedRequest request, CancellationToken ctk = default)
        {
            var processor = serviceProvider.GetRequiredService<IRequestProcessor>();
            var innerScopeId = await processor.ExecuteAsync(new InnerNestedRequest(), ctk).ConfigureAwait(false);
            return new NestedScopeResult(scopeProbe.InstanceId, innerScopeId);
        }
    }

    private sealed class InnerNestedRequestHandler(ScopeProbe scopeProbe) : IRequestHandler<InnerNestedRequest, Guid>
    {
        public async Task<Guid> ExecuteAsync(InnerNestedRequest request, CancellationToken ctk = default)
        {
            return await Task.FromResult(scopeProbe.InstanceId).ConfigureAwait(false);
        }
    }

    private sealed class ScopedTrackingRequestProcessor : IRequestProcessor
    {
        private readonly ScopeProbe _scopeProbe;
        private readonly SimpleInjectorRequestProcessor _inner;

        public ScopedTrackingRequestProcessor(Container container, ScopeProbe scopeProbe)
        {
            _scopeProbe = scopeProbe;
            _inner = new SimpleInjectorRequestProcessor(container);
            ProcessorInstanceId = Guid.NewGuid();
        }

        public Guid ProcessorInstanceId { get; }

        [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
#pragma warning disable CS0618 // Type or member is obsolete
        public TResponse Execute<TResponse>(IRequest<TResponse> request)
        {
            throw new NotSupportedException("Synchronous execution is not supported. Use ExecuteAsync instead.");
        }
#pragma warning restore CS0618 // Type or member is obsolete

        public async Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default)
        {
            return await _inner.ExecuteAsync(request, ctk).ConfigureAwait(false);
        }

        public async Task<TResponse> ExecuteAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request, CancellationToken ctk = default)
            where TRequest : class, IRequest<TRequest, TResponse>
        {
            if (request is ScopedProcessorRequest)
            {
                return await Task.FromResult((TResponse)(object)new ScopedProcessorObservation(ProcessorInstanceId, _scopeProbe.InstanceId)).ConfigureAwait(false);
            }

            return await _inner.ExecuteAsync<TRequest, TResponse>(request, ctk).ConfigureAwait(false);
        }
    }

    private sealed class TestQueryHandler(Trace trace) : IQueryHandler<TestQuery, int>
    {
        public async Task<int> ExecuteAsync(TestQuery query, CancellationToken ctk = default)
        {
            trace.Events.Add("query-handler");
            return await Task.FromResult(query.Value).ConfigureAwait(false);
        }
    }

    private sealed class CancellableQueryHandler : IQueryHandler<CancellableQuery, int>
    {
        public async Task<int> ExecuteAsync(CancellableQuery query, CancellationToken ctk = default)
        {
            return await Task.FromCanceled<int>(ctk).ConfigureAwait(false);
        }
    }

    private sealed class TestCommandHandler(Trace trace) : ICommandHandler<TestCommand>
    {
        public async Task ExecuteAsync(TestCommand command, CancellationToken ctk = default)
        {
            trace.Events.Add("command-handler");
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private sealed class RequestDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> decoratee, Trace trace) : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default)
        {
            trace.Events.Add("request-decorator");
            return await decoratee.ExecuteAsync(request, ctk).ConfigureAwait(false);
        }
    }

    private sealed class QueryDecorator<TQuery, TResult>(IQueryHandler<TQuery, TResult> decoratee, Trace trace) : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
        {
            trace.Events.Add("query-decorator");
            return await decoratee.ExecuteAsync(query, ctk).ConfigureAwait(false);
        }
    }

    private sealed class CommandDecorator<TCommand>(ICommandHandler<TCommand> decoratee, Trace trace) : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task ExecuteAsync(TCommand command, CancellationToken ctk = default)
        {
            trace.Events.Add("command-decorator");
            await decoratee.ExecuteAsync(command, ctk).ConfigureAwait(false);
        }
    }
}
