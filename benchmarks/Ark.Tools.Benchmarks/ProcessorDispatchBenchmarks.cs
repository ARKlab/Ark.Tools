// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using BenchmarkDotNet.Attributes;

using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;

using SimpleInjector;

namespace Ark.Tools.Benchmarks;

[MemoryDiagnoser]
public class ProcessorDispatchBenchmarks : IDisposable
{
    private Container _container = null!;
    private IQueryProcessor _queryProcessor = null!;
    private IRequestProcessor _requestProcessor = null!;
    private ICommandProcessor _commandProcessor = null!;
    private BenchmarkQuery _query = null!;
    private BenchmarkRequest _request = null!;
    private BenchmarkCommand _command = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = new Container();
        _container.Register<IQueryHandler<BenchmarkQuery, int>, BenchmarkQueryHandler>();
        _container.Register<IRequestHandler<BenchmarkRequest, int>, BenchmarkRequestHandler>();
        _container.Register<ICommandHandler<BenchmarkCommand>, BenchmarkCommandHandler>();
        _container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(QueryDecorator<,>));
        _container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestDecorator<,>));
        _container.RegisterDecorator(typeof(ICommandHandler<>), typeof(CommandDecorator<>));
        _container.Verify();

        _queryProcessor = new SimpleInjectorQueryProcessor(_container);
        _requestProcessor = new SimpleInjectorRequestProcessor(_container);
        _commandProcessor = new SimpleInjectorCommandProcessor(_container);
        _query = new BenchmarkQuery();
        _request = new BenchmarkRequest();
        _command = new BenchmarkCommand();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _container.Dispose();
        }
    }

    [Benchmark]
    public async Task<int> Query_reflection_dynamic()
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(_query.GetType(), typeof(int));
        dynamic handler = _container.GetInstance(handlerType);
        return await handler.ExecuteAsync((dynamic)_query);
    }

    [Benchmark]
    public async Task<int> Query_cached_invoker()
    {
        return await _queryProcessor.ExecuteAsync(_query).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> Request_reflection_dynamic()
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(_request.GetType(), typeof(int));
        dynamic handler = _container.GetInstance(handlerType);
        return await handler.ExecuteAsync((dynamic)_request);
    }

    [Benchmark]
    public async Task<int> Request_cached_invoker()
    {
        return await _requestProcessor.ExecuteAsync(_request).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Command_reflection_dynamic()
    {
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(_command.GetType());
        dynamic handler = _container.GetInstance(handlerType);
        await handler.ExecuteAsync((dynamic)_command);
    }

    [Benchmark]
    public async Task Command_cached_invoker()
    {
        await _commandProcessor.ExecuteAsync(_command).ConfigureAwait(false);
    }

    private sealed record BenchmarkQuery : IQuery<int>;
    private sealed record BenchmarkRequest : IRequest<int>;
    private sealed record BenchmarkCommand : ICommand;

    private sealed class BenchmarkQueryHandler : IQueryHandler<BenchmarkQuery, int>
    {
        public Task<int> ExecuteAsync(BenchmarkQuery query, CancellationToken ctk = default)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class BenchmarkRequestHandler : IRequestHandler<BenchmarkRequest, int>
    {
        public Task<int> ExecuteAsync(BenchmarkRequest request, CancellationToken ctk = default)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class BenchmarkCommandHandler : ICommandHandler<BenchmarkCommand>
    {
        public Task ExecuteAsync(BenchmarkCommand command, CancellationToken ctk = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class QueryDecorator<TQuery, TResult>(IQueryHandler<TQuery, TResult> decoratee) : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
        {
            return await decoratee.ExecuteAsync(query, ctk).ConfigureAwait(false);
        }
    }

    private sealed class RequestDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> decoratee) : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default)
        {
            return await decoratee.ExecuteAsync(request, ctk).ConfigureAwait(false);
        }
    }

    private sealed class CommandDecorator<TCommand>(ICommandHandler<TCommand> decoratee) : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task ExecuteAsync(TCommand command, CancellationToken ctk = default)
        {
            await decoratee.ExecuteAsync(command, ctk).ConfigureAwait(false);
        }
    }
}
