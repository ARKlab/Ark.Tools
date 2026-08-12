// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

using SimpleInjector;

namespace Ark.Tools.Solid.SimpleInjector;

internal static class QueryHandlerInvokerCache<TResult>
{
    private static readonly MethodInfo _getInstance = typeof(Container).GetMethod(nameof(Container.GetInstance), [typeof(Type)])!;
    private static readonly ConcurrentDictionary<Type, Func<Container, object, CancellationToken, Task<TResult>>> _invokers = new();

    [RequiresUnreferencedCode("Builds a runtime handler invoker. Handler types must be preserved by the processor contract.")]
    public static Task<TResult> ExecuteAsync(Container container, IQuery<TResult> query, CancellationToken cancellationToken)
    {
        var invoker = _invokers.GetOrAdd(query.GetType(), static queryType => _createInvoker(queryType));
        return invoker(container, query, cancellationToken);
    }

    [RequiresUnreferencedCode("Builds a runtime handler invoker. Handler types must be preserved by the processor contract.")]
    private static Func<Container, object, CancellationToken, Task<TResult>> _createInvoker(Type queryType)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));
        var container = Expression.Parameter(typeof(Container), "container");
        var query = Expression.Parameter(typeof(object), "query");
        var cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var handler = Expression.Convert(
            Expression.Call(container, _getInstance, Expression.Constant(handlerType)),
            handlerType);
        var execute = Expression.Call(
            handler,
            nameof(IQueryHandler<IQuery<TResult>, TResult>.ExecuteAsync),
            Type.EmptyTypes,
            Expression.Convert(query, queryType),
            cancellationToken);

        return Expression.Lambda<Func<Container, object, CancellationToken, Task<TResult>>>(
            execute,
            container,
            query,
            cancellationToken).Compile();
    }
}

internal static class RequestHandlerInvokerCache<TResponse>
{
    private static readonly MethodInfo _getInstance = typeof(Container).GetMethod(nameof(Container.GetInstance), [typeof(Type)])!;
    private static readonly ConcurrentDictionary<Type, Func<Container, object, CancellationToken, Task<TResponse>>> _invokers = new();

    [RequiresUnreferencedCode("Builds a runtime handler invoker. Handler types must be preserved by the processor contract.")]
    public static Task<TResponse> ExecuteAsync(Container container, IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        var invoker = _invokers.GetOrAdd(request.GetType(), static requestType => _createInvoker(requestType));
        return invoker(container, request, cancellationToken);
    }

    [RequiresUnreferencedCode("Builds a runtime handler invoker. Handler types must be preserved by the processor contract.")]
    private static Func<Container, object, CancellationToken, Task<TResponse>> _createInvoker(Type requestType)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var container = Expression.Parameter(typeof(Container), "container");
        var request = Expression.Parameter(typeof(object), "request");
        var cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var handler = Expression.Convert(
            Expression.Call(container, _getInstance, Expression.Constant(handlerType)),
            handlerType);
        var execute = Expression.Call(
            handler,
            nameof(IRequestHandler<IRequest<TResponse>, TResponse>.ExecuteAsync),
            Type.EmptyTypes,
            Expression.Convert(request, requestType),
            cancellationToken);

        return Expression.Lambda<Func<Container, object, CancellationToken, Task<TResponse>>>(
            execute,
            container,
            request,
            cancellationToken).Compile();
    }
}

internal static class CommandHandlerInvokerCache
{
    private static readonly MethodInfo _getInstance = typeof(Container).GetMethod(nameof(Container.GetInstance), [typeof(Type)])!;
    private static readonly ConcurrentDictionary<Type, Func<Container, object, CancellationToken, Task>> _invokers = new();

    [RequiresUnreferencedCode("Builds a runtime handler invoker. Handler types must be preserved by the processor contract.")]
    public static Task ExecuteAsync(Container container, ICommand command, CancellationToken cancellationToken)
    {
        var invoker = _invokers.GetOrAdd(command.GetType(), static commandType => _createInvoker(commandType));
        return invoker(container, command, cancellationToken);
    }

    [RequiresUnreferencedCode("Builds a runtime handler invoker. Handler types must be preserved by the processor contract.")]
    private static Func<Container, object, CancellationToken, Task> _createInvoker(Type commandType)
    {
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
        var container = Expression.Parameter(typeof(Container), "container");
        var command = Expression.Parameter(typeof(object), "command");
        var cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var handler = Expression.Convert(
            Expression.Call(container, _getInstance, Expression.Constant(handlerType)),
            handlerType);
        var execute = Expression.Call(
            handler,
            nameof(ICommandHandler<ICommand>.ExecuteAsync),
            Type.EmptyTypes,
            Expression.Convert(command, commandType),
            cancellationToken);

        return Expression.Lambda<Func<Container, object, CancellationToken, Task>>(
            execute,
            container,
            command,
            cancellationToken).Compile();
    }
}
