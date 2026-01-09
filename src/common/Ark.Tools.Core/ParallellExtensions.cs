// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Ark.Tools.Core;

public static class ParallellExtensions
{
    public static Task Parallel<T>(this IList<T> list, int degree, Func<T, Task> action, CancellationToken ctk = default)
    {
        return list.Parallel(degree, (i, x, ct) => action.Invoke(x), ctk);
    }
    public static Task<IList<TResult>> Parallel<T, TResult>(this IList<T> list, int degree, Func<T, Task<TResult>> action, CancellationToken ctk = default)
    {
        return list.Parallel(degree, (i, x, ct) => action.Invoke(x), ctk);
    }

    public static Task Parallel<T>(this IList<T> list, int degree, Func<int, T, Task> action, CancellationToken ctk = default)
    {
        return list.Parallel(degree, (i, x, ct) => action.Invoke(i, x), ctk);
    }

    public static Task<IList<TResult>> Parallel<T, TResult>(this IList<T> list, int degree, Func<int, T, Task<TResult>> action, CancellationToken ctk = default)
    {
        return list.Parallel(degree, (i, x, ct) => action.Invoke(i, x), ctk);
    }

    public static Task Parallel<T>(this IList<T> list, int degree, Func<int, T, CancellationToken, Task> action, CancellationToken ctk = default)
    {
        return list.ToObservable()
            .Select((x, i) => Observable.FromAsync(ct => action(i, x, ct)).SubscribeOn(DefaultScheduler.Instance))
            .Merge(degree)
            .ToList()
            .ToTask(ctk);
    }

    public static Task<IList<TResult>> Parallel<T, TResult>(this IList<T> list, int degree, Func<int, T, CancellationToken, Task<TResult>> action, CancellationToken ctk = default)
    {
        return list.ToObservable()
            .Select((x, i) => Observable.FromAsync(ct => action(i, x, ct)).SubscribeOn(DefaultScheduler.Instance))
            .Merge(degree)
            .ToList()
            .ToTask(ctk);
    }
}