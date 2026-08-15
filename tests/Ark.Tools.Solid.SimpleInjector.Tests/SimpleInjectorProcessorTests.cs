// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using SimpleInjector;

namespace Ark.Tools.Solid.SimpleInjector.Tests;

[TestClass]
public sealed class SimpleInjectorProcessorTests
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
    public async Task Processors_execute_decorated_handlers()
    {
        await using var container = _createContainer();
        var trace = container.GetInstance<Trace>();
        var requestProcessor = new SimpleInjectorRequestProcessor(container);
        var queryProcessor = new SimpleInjectorQueryProcessor(container);
        var commandProcessor = new SimpleInjectorCommandProcessor(container);

        var requestResult = await requestProcessor.ExecuteAsync(new TestRequest(1)).ConfigureAwait(false);
        var queryResult = await queryProcessor.ExecuteAsync(new TestQuery(2)).ConfigureAwait(false);
        await commandProcessor.ExecuteAsync(new TestCommand(3)).ConfigureAwait(false);

        Assert.AreEqual(1, requestResult);
        Assert.AreEqual(2, queryResult);
        CollectionAssert.AreEqual(
            _expectedEvents,
            trace.Events);
    }

    [TestMethod]
    public async Task Processors_propagate_cancellation_and_exceptions()
    {
        await using var container = _createContainer();
        var queryProcessor = new SimpleInjectorQueryProcessor(container);
        var requestProcessor = new SimpleInjectorRequestProcessor(container);
        var cancellationToken = new CancellationToken(canceled: true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await queryProcessor.ExecuteAsync(new CancellableQuery(), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await requestProcessor.ExecuteAsync(new FailingRequest(), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Processors_execute_self_generic_types_without_reflection()
    {
        await using var container = _createContainer();
        var trace = container.GetInstance<Trace>();
        var requestProcessor = new SimpleInjectorRequestProcessor(container);
        var queryProcessor = new SimpleInjectorQueryProcessor(container);
        var commandProcessor = new SimpleInjectorCommandProcessor(container);

        var requestResult = await requestProcessor.ExecuteAsync(new SelfRequest(4)).ConfigureAwait(false);
        var queryResult = await queryProcessor.ExecuteAsync(new SelfQuery(5)).ConfigureAwait(false);
        await commandProcessor.ExecuteAsync(new SelfCommand(6)).ConfigureAwait(false);

        Assert.AreEqual(4, requestResult);
        Assert.AreEqual(5, queryResult);
        CollectionAssert.AreEqual(
            _expectedEvents,
            trace.Events);
    }

    private static Container _createContainer()
    {
        var container = new Container();
        container.RegisterInstance(new Trace());
        container.Register<IRequestHandler<TestRequest, int>, TestRequestHandler>();
        container.Register<IRequestHandler<FailingRequest, int>, FailingRequestHandler>();
        container.Register<IRequestHandler<SelfRequest, int>, SelfRequestHandler>();
        container.Register<IQueryHandler<TestQuery, int>, TestQueryHandler>();
        container.Register<IQueryHandler<CancellableQuery, int>, CancellableQueryHandler>();
        container.Register<IQueryHandler<SelfQuery, int>, SelfQueryHandler>();
        container.Register<ICommandHandler<TestCommand>, TestCommandHandler>();
        container.Register<ICommandHandler<SelfCommand>, SelfCommandHandler>();
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestDecorator<,>));
        container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(QueryDecorator<,>));
        container.RegisterDecorator(typeof(ICommandHandler<>), typeof(CommandDecorator<>));
        container.Verify();
        return container;
    }

    private sealed class Trace
    {
        public List<string> Events { get; } = [];
    }

#pragma warning disable ARKSOLID001 // Legacy types intentionally test the reflective dispatch path
    private sealed record TestRequest(int Value) : IRequest<int>;
    private sealed record FailingRequest : IRequest<int>;
    private sealed record TestQuery(int Value) : IQuery<int>;
    private sealed record CancellableQuery : IQuery<int>;
    private sealed record TestCommand(int Value) : ICommand;
#pragma warning restore ARKSOLID001
    private sealed record SelfRequest(int Value) : IRequest<SelfRequest, int>;
    private sealed record SelfQuery(int Value) : IQuery<SelfQuery, int>;
    private sealed record SelfCommand(int Value) : ICommand<SelfCommand>;

    private sealed class SelfRequestHandler(Trace trace) : IRequestHandler<SelfRequest, int>
    {
        public Task<int> ExecuteAsync(SelfRequest request, CancellationToken ctk = default)
        {
            trace.Events.Add("request-handler");
            return Task.FromResult(request.Value);
        }
    }

    private sealed class SelfQueryHandler(Trace trace) : IQueryHandler<SelfQuery, int>
    {
        public Task<int> ExecuteAsync(SelfQuery query, CancellationToken ctk = default)
        {
            trace.Events.Add("query-handler");
            return Task.FromResult(query.Value);
        }
    }

    private sealed class SelfCommandHandler(Trace trace) : ICommandHandler<SelfCommand>
    {
        public Task ExecuteAsync(SelfCommand command, CancellationToken ctk = default)
        {
            trace.Events.Add("command-handler");
            return Task.CompletedTask;
        }
    }

    private sealed class TestRequestHandler(Trace trace) : IRequestHandler<TestRequest, int>
    {
        public Task<int> ExecuteAsync(TestRequest request, CancellationToken ctk = default)
        {
            trace.Events.Add("request-handler");
            return Task.FromResult(request.Value);
        }
    }

    private sealed class FailingRequestHandler : IRequestHandler<FailingRequest, int>
    {
        public Task<int> ExecuteAsync(FailingRequest request, CancellationToken ctk = default)
        {
            return Task.FromException<int>(new InvalidOperationException());
        }
    }

    private sealed class TestQueryHandler(Trace trace) : IQueryHandler<TestQuery, int>
    {
        public Task<int> ExecuteAsync(TestQuery query, CancellationToken ctk = default)
        {
            trace.Events.Add("query-handler");
            return Task.FromResult(query.Value);
        }
    }

    private sealed class CancellableQueryHandler : IQueryHandler<CancellableQuery, int>
    {
        public Task<int> ExecuteAsync(CancellableQuery query, CancellationToken ctk = default)
        {
            return Task.FromCanceled<int>(ctk);
        }
    }

    private sealed class TestCommandHandler(Trace trace) : ICommandHandler<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken ctk = default)
        {
            trace.Events.Add("command-handler");
            return Task.CompletedTask;
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
