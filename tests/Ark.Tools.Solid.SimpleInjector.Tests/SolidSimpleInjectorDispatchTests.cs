// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;
using AwesomeAssertions;
using SimpleInjector;

namespace Ark.Tools.Solid.SimpleInjector.Tests;

[TestClass]
public sealed class SolidSimpleInjectorDispatchTests
{
    [TestMethod]
    public async Task GeneratedDispatchPreservesDecoratorOrderForAllContractKinds()
    {
        var calls = new List<string>();
        using var container = CreateContainer(calls);
        var dispatcher = new TestDispatcher();

        var requestResult = await SimpleInjectorRequestProcessor.Create(container, dispatcher)
            .ExecuteAsync(new TestRequest("request"), CancellationToken.None);
        var queryResult = await SimpleInjectorQueryProcessor.Create(container, dispatcher)
            .ExecuteAsync(new TestQuery("query"), CancellationToken.None);
        await SimpleInjectorCommandProcessor.Create(container, dispatcher)
            .ExecuteAsync(new TestCommand("command"), CancellationToken.None);

        requestResult.Should().Be("request-handled");
        queryResult.Should().Be("query-handled");
        calls.Should().Equal(
            "request-outer-before", "request-inner-before", "request-handler", "request-inner-after", "request-outer-after",
            "query-outer-before", "query-inner-before", "query-handler", "query-inner-after", "query-outer-after",
            "command-outer-before", "command-inner-before", "command-handler", "command-inner-after", "command-outer-after");
    }

    [TestMethod]
    public async Task GeneratedDispatchPropagatesCancellationAndExceptions()
    {
        var calls = new List<string>();
        using var container = CreateContainer(calls);
        var dispatcher = new TestDispatcher();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = async () => await SimpleInjectorRequestProcessor.Create(container, dispatcher)
            .ExecuteAsync(new TestRequest("cancel"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        var exceptionAct = async () => await SimpleInjectorQueryProcessor.Create(container, dispatcher)
            .ExecuteAsync(new TestQuery("error"), CancellationToken.None);

        await exceptionAct.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestMethod]
    public async Task UnknownContractUsesCompatibilityFallback()
    {
        using var container = new Container();
        container.Register<IRequestHandler<UnknownRequest, string>, UnknownRequestHandler>();
        container.Verify();

        var result = await new SimpleInjectorRequestProcessor(container)
            .ExecuteAsync(new UnknownRequest(), CancellationToken.None);

        result.Should().Be("fallback");
    }

    private static Container CreateContainer(List<string> calls)
    {
        var container = new Container();
        container.Register<IRequestHandler<TestRequest, string>, TestRequestHandler>();
        container.Register<IQueryHandler<TestQuery, string>, TestQueryHandler>();
        container.Register<ICommandHandler<TestCommand>, TestCommandHandler>();
        container.RegisterDecorator<IRequestHandler<TestRequest, string>, RequestInnerDecorator>();
        container.RegisterDecorator<IRequestHandler<TestRequest, string>, RequestOuterDecorator>();
        container.RegisterDecorator<IQueryHandler<TestQuery, string>, QueryInnerDecorator>();
        container.RegisterDecorator<IQueryHandler<TestQuery, string>, QueryOuterDecorator>();
        container.RegisterDecorator<ICommandHandler<TestCommand>, CommandInnerDecorator>();
        container.RegisterDecorator<ICommandHandler<TestCommand>, CommandOuterDecorator>();
        container.Options.EnableAutoVerification = false;
        DecoratorCalls.Set(calls);
        container.Verify();
        return container;
    }

    private static class DecoratorCalls
    {
        private static List<string>? _calls;

        public static void Set(List<string> calls) => _calls = calls;

        public static void Add(string call) => _calls!.Add(call);
    }

    [GenerateSolidSimpleInjectorDispatch]
    internal sealed record TestRequest(string Value) : IRequest<string>;
    [GenerateSolidSimpleInjectorDispatch]
    internal sealed record TestQuery(string Value) : IQuery<string>;
    [GenerateSolidSimpleInjectorDispatch]
    internal sealed record TestCommand(string Value) : ICommand;
    public sealed record UnknownRequest : IRequest<string>;

    internal sealed class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public async Task<string> ExecuteAsync(TestRequest request, CancellationToken ctk = default)
        {
            ctk.ThrowIfCancellationRequested();
            DecoratorCalls.Add("request-handler");
            await Task.CompletedTask;
            return request.Value + "-handled";
        }
    }

    internal sealed class TestQueryHandler : IQueryHandler<TestQuery, string>
    {
        public async Task<string> ExecuteAsync(TestQuery query, CancellationToken ctk = default)
        {
            ctk.ThrowIfCancellationRequested();
            if (query.Value == "error")
                throw new InvalidOperationException("query failed");
            DecoratorCalls.Add("query-handler");
            await Task.CompletedTask;
            return query.Value + "-handled";
        }
    }

    internal sealed class TestCommandHandler : ICommandHandler<TestCommand>
    {
        public async Task ExecuteAsync(TestCommand command, CancellationToken ctk = default)
        {
            ctk.ThrowIfCancellationRequested();
            DecoratorCalls.Add("command-handler");
            await Task.CompletedTask;
        }
    }

    public sealed class UnknownRequestHandler : IRequestHandler<UnknownRequest, string>
    {
        public async Task<string> ExecuteAsync(UnknownRequest request, CancellationToken ctk = default)
        {
            await Task.CompletedTask;
            return "fallback";
        }
    }

    private sealed class RequestInnerDecorator : IRequestHandler<TestRequest, string>
    {
        private readonly IRequestHandler<TestRequest, string> _decorated;

        public RequestInnerDecorator(IRequestHandler<TestRequest, string> decorated) => _decorated = decorated;

        public async Task<string> ExecuteAsync(TestRequest request, CancellationToken ctk = default)
        {
            DecoratorCalls.Add("request-inner-before");
            var result = await _decorated.ExecuteAsync(request, ctk);
            DecoratorCalls.Add("request-inner-after");
            return result;
        }
    }

    private sealed class RequestOuterDecorator : IRequestHandler<TestRequest, string>
    {
        private readonly IRequestHandler<TestRequest, string> _decorated;

        public RequestOuterDecorator(IRequestHandler<TestRequest, string> decorated) => _decorated = decorated;

        public async Task<string> ExecuteAsync(TestRequest request, CancellationToken ctk = default)
        {
            DecoratorCalls.Add("request-outer-before");
            var result = await _decorated.ExecuteAsync(request, ctk);
            DecoratorCalls.Add("request-outer-after");
            return result;
        }
    }

    private sealed class QueryInnerDecorator : IQueryHandler<TestQuery, string>
    {
        private readonly IQueryHandler<TestQuery, string> _decorated;

        public QueryInnerDecorator(IQueryHandler<TestQuery, string> decorated) => _decorated = decorated;

        public async Task<string> ExecuteAsync(TestQuery query, CancellationToken ctk = default)
        {
            DecoratorCalls.Add("query-inner-before");
            var result = await _decorated.ExecuteAsync(query, ctk);
            DecoratorCalls.Add("query-inner-after");
            return result;
        }
    }

    private sealed class QueryOuterDecorator : IQueryHandler<TestQuery, string>
    {
        private readonly IQueryHandler<TestQuery, string> _decorated;

        public QueryOuterDecorator(IQueryHandler<TestQuery, string> decorated) => _decorated = decorated;

        public async Task<string> ExecuteAsync(TestQuery query, CancellationToken ctk = default)
        {
            DecoratorCalls.Add("query-outer-before");
            var result = await _decorated.ExecuteAsync(query, ctk);
            DecoratorCalls.Add("query-outer-after");
            return result;
        }
    }

    private sealed class CommandInnerDecorator : ICommandHandler<TestCommand>
    {
        private readonly ICommandHandler<TestCommand> _decorated;

        public CommandInnerDecorator(ICommandHandler<TestCommand> decorated) => _decorated = decorated;

        public async Task ExecuteAsync(TestCommand command, CancellationToken ctk = default)
        {
            DecoratorCalls.Add("command-inner-before");
            await _decorated.ExecuteAsync(command, ctk);
            DecoratorCalls.Add("command-inner-after");
        }
    }

    private sealed class CommandOuterDecorator : ICommandHandler<TestCommand>
    {
        private readonly ICommandHandler<TestCommand> _decorated;

        public CommandOuterDecorator(ICommandHandler<TestCommand> decorated) => _decorated = decorated;

        public async Task ExecuteAsync(TestCommand command, CancellationToken ctk = default)
        {
            DecoratorCalls.Add("command-outer-before");
            await _decorated.ExecuteAsync(command, ctk);
            DecoratorCalls.Add("command-outer-after");
        }
    }

    private sealed class TestDispatcher : ISolidSimpleInjectorDispatcher
    {
        public bool TryExecuteRequest<TResponse>(
            Container container,
            IRequest<TResponse> request,
            CancellationToken ctk,
            out Task<TResponse>? execution)
        {
            if (request is TestRequest typedRequest)
            {
                execution = (Task<TResponse>)(object)container
                    .GetInstance<IRequestHandler<TestRequest, string>>()
                    .ExecuteAsync(typedRequest, ctk);
                return true;
            }

            execution = null;
            return false;
        }

        public bool TryExecuteQuery<TResult>(
            Container container,
            IQuery<TResult> query,
            CancellationToken ctk,
            out Task<TResult>? execution)
        {
            if (query is TestQuery typedQuery)
            {
                execution = (Task<TResult>)(object)container
                    .GetInstance<IQueryHandler<TestQuery, string>>()
                    .ExecuteAsync(typedQuery, ctk);
                return true;
            }

            execution = null;
            return false;
        }

        public bool TryExecuteCommand(
            Container container,
            ICommand command,
            CancellationToken ctk,
            out Task? execution)
        {
            if (command is TestCommand typedCommand)
            {
                execution = container
                    .GetInstance<ICommandHandler<TestCommand>>()
                    .ExecuteAsync(typedCommand, ctk);
                return true;
            }

            execution = null;
            return false;
        }
    }
}
