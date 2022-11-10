// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Core
{
    public static class ParallellExtensions
    {
        public static Task Parallel<T>(this IList<T> list, int degree, Func<T, Task> action, CancellationToken ctk = default)
        {
            return list.Parallel(degree, (i, x, ct) => action.Invoke(x), ctk);
        }
        public static Task<IList<U>> Parallel<T,U>(this IList<T> list, int degree, Func<T, Task<U>> action, CancellationToken ctk = default)
        {
            return list.Parallel(degree, (i, x, ct) => action.Invoke(x), ctk);
        }

        public static Task Parallel<T>(this IList<T> list, int degree, Func<int, T, Task> action, CancellationToken ctk = default)
        {
            return list.Parallel(degree, (i, x, ct) => action.Invoke(i, x), ctk);
        }

        public static Task<IList<U>> Parallel<T, U>(this IList<T> list, int degree, Func<int, T, Task<U>> action, CancellationToken ctk = default)
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

        public static Task<IList<U>> Parallel<T,U>(this IList<T> list, int degree, Func<int, T, CancellationToken, Task<U>> action, CancellationToken ctk = default)
        {
            return list.ToObservable()
                .Select((x, i) => Observable.FromAsync(ct => action(i, x, ct)).SubscribeOn(DefaultScheduler.Instance))
                .Merge(degree)
                .ToList()
                .ToTask(ctk);
        }
    }
}
