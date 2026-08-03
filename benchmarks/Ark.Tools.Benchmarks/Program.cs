// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Ark.Tools.Benchmarks;

public static class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<SolidDispatchBenchmarks>();
    }
}

[MemoryDiagnoser]
[InProcess]
public class SolidDispatchBenchmarks : IDisposable
{
    private readonly global::SimpleInjector.Container _container;
    private readonly Ark.Tools.Solid.SimpleInjector.SimpleInjectorRequestProcessor _fallbackRequest;
    private readonly Ark.Tools.Solid.SimpleInjector.SimpleInjectorQueryProcessor _fallbackQuery;
    private readonly Ark.Tools.Solid.SimpleInjector.SimpleInjectorCommandProcessor _fallbackCommand;
    private readonly Ark.Tools.Solid.SimpleInjector.SimpleInjectorRequestProcessor _generatedRequest;
    private readonly Ark.Tools.Solid.SimpleInjector.SimpleInjectorQueryProcessor _generatedQuery;
    private readonly Ark.Tools.Solid.SimpleInjector.SimpleInjectorCommandProcessor _generatedCommand;
    private readonly TestRequest _request = new();
    private readonly TestQuery _query = new();
    private readonly TestCommand _command = new();

    public SolidDispatchBenchmarks()
    {
        _container = new global::SimpleInjector.Container();
        _container.Register<Ark.Tools.Solid.IRequestHandler<TestRequest, int>, RequestHandler>();
        _container.Register<Ark.Tools.Solid.IQueryHandler<TestQuery, int>, QueryHandler>();
        _container.Register<Ark.Tools.Solid.ICommandHandler<TestCommand>, CommandHandler>();
        _container.RegisterDecorator<Ark.Tools.Solid.IRequestHandler<TestRequest, int>, RequestDecorator>();
        _container.RegisterDecorator<Ark.Tools.Solid.IQueryHandler<TestQuery, int>, QueryDecorator>();
        _container.RegisterDecorator<Ark.Tools.Solid.ICommandHandler<TestCommand>, CommandDecorator>();
        _container.Verify();

        _fallbackRequest = new(_container);
        _fallbackQuery = new(_container);
        _fallbackCommand = new(_container);
        var dispatcher = new TestDispatcher();
        _generatedRequest = new(_container, dispatcher);
        _generatedQuery = new(_container, dispatcher);
        _generatedCommand = new(_container, dispatcher);
    }

    [Benchmark(Baseline = true)]
    public Task<int> RequestFallback() => _fallbackRequest.ExecuteAsync(_request);

    [Benchmark]
    public Task<int> RequestGenerated() => _generatedRequest.ExecuteAsync(_request);

    [Benchmark]
    public Task<int> QueryFallback() => _fallbackQuery.ExecuteAsync(_query);

    [Benchmark]
    public Task<int> QueryGenerated() => _generatedQuery.ExecuteAsync(_query);

    [Benchmark]
    public Task CommandFallback() => _fallbackCommand.ExecuteAsync(_command);

    [Benchmark]
    public Task CommandGenerated() => _generatedCommand.ExecuteAsync(_command);

    public void Dispose()
    {
        _container.Dispose();
        GC.SuppressFinalize(this);
    }

    public sealed class TestRequest : Ark.Tools.Solid.IRequest<int>;
    public sealed class TestQuery : Ark.Tools.Solid.IQuery<int>;
    public sealed class TestCommand : Ark.Tools.Solid.ICommand;

    public sealed class RequestHandler : Ark.Tools.Solid.IRequestHandler<TestRequest, int>
    {
        public Task<int> ExecuteAsync(TestRequest request, CancellationToken ctk = default) => Task.FromResult(42);
    }

    public sealed class QueryHandler : Ark.Tools.Solid.IQueryHandler<TestQuery, int>
    {
        public Task<int> ExecuteAsync(TestQuery query, CancellationToken ctk = default) => Task.FromResult(42);
    }

    public sealed class CommandHandler : Ark.Tools.Solid.ICommandHandler<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken ctk = default) => Task.CompletedTask;
    }

    public sealed class RequestDecorator : Ark.Tools.Solid.IRequestHandler<TestRequest, int>
    {
        private readonly Ark.Tools.Solid.IRequestHandler<TestRequest, int> _decorated;
        public RequestDecorator(Ark.Tools.Solid.IRequestHandler<TestRequest, int> decorated) => _decorated = decorated;
        public Task<int> ExecuteAsync(TestRequest request, CancellationToken ctk = default) => _decorated.ExecuteAsync(request, ctk);
    }

    public sealed class QueryDecorator : Ark.Tools.Solid.IQueryHandler<TestQuery, int>
    {
        private readonly Ark.Tools.Solid.IQueryHandler<TestQuery, int> _decorated;
        public QueryDecorator(Ark.Tools.Solid.IQueryHandler<TestQuery, int> decorated) => _decorated = decorated;
        public Task<int> ExecuteAsync(TestQuery query, CancellationToken ctk = default) => _decorated.ExecuteAsync(query, ctk);
    }

    public sealed class CommandDecorator : Ark.Tools.Solid.ICommandHandler<TestCommand>
    {
        private readonly Ark.Tools.Solid.ICommandHandler<TestCommand> _decorated;
        public CommandDecorator(Ark.Tools.Solid.ICommandHandler<TestCommand> decorated) => _decorated = decorated;
        public Task ExecuteAsync(TestCommand command, CancellationToken ctk = default) => _decorated.ExecuteAsync(command, ctk);
    }

    private sealed class TestDispatcher : Ark.Tools.Solid.SimpleInjector.ISolidSimpleInjectorDispatcher
    {
        public bool TryExecuteRequest<TResponse>(global::SimpleInjector.Container container, Ark.Tools.Solid.IRequest<TResponse> request, CancellationToken ctk, out Task<TResponse>? execution)
        {
            if (request is TestRequest typedRequest)
            {
                execution = (Task<TResponse>)(object)container.GetInstance<Ark.Tools.Solid.IRequestHandler<TestRequest, int>>().ExecuteAsync(typedRequest, ctk);
                return true;
            }
            execution = null;
            return false;
        }

        public bool TryExecuteQuery<TResult>(global::SimpleInjector.Container container, Ark.Tools.Solid.IQuery<TResult> query, CancellationToken ctk, out Task<TResult>? execution)
        {
            if (query is TestQuery typedQuery)
            {
                execution = (Task<TResult>)(object)container.GetInstance<Ark.Tools.Solid.IQueryHandler<TestQuery, int>>().ExecuteAsync(typedQuery, ctk);
                return true;
            }
            execution = null;
            return false;
        }

        public bool TryExecuteCommand(global::SimpleInjector.Container container, Ark.Tools.Solid.ICommand command, CancellationToken ctk, out Task? execution)
        {
            if (command is TestCommand typedCommand)
            {
                execution = container.GetInstance<Ark.Tools.Solid.ICommandHandler<TestCommand>>().ExecuteAsync(typedCommand, ctk);
                return true;
            }
            execution = null;
            return false;
        }
    }
}
