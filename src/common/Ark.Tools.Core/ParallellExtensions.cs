// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Runtime.CompilerServices;

namespace Ark.Tools.Core;

public static class ParallellExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Parallel<T>(this IList<T> list, int degree, Func<T, Task> action, CancellationToken ctk = default)
    {
        return list.Parallel(degree, (i, x, ct) => action.Invoke(x), ctk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<IList<TResult>> Parallel<T, TResult>(this IList<T> list, int degree, Func<T, Task<TResult>> action, CancellationToken ctk = default)
    {
        return list.Parallel(degree, (i, x, ct) => action.Invoke(x), ctk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Parallel<T>(this IList<T> list, int degree, Func<int, T, Task> action, CancellationToken ctk = default)
    {
        return list.Parallel(degree, (i, x, ct) => action.Invoke(i, x), ctk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<IList<TResult>> Parallel<T, TResult>(this IList<T> list, int degree, Func<int, T, Task<TResult>> action, CancellationToken ctk = default)
    {
        return list.Parallel(degree, (i, x, ct) => action.Invoke(i, x), ctk);
    }

    public static async Task Parallel<T>(this IList<T> list, int degree, Func<int, T, CancellationToken, Task> action, CancellationToken ctk = default)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degree,
            CancellationToken = ctk
        };

        await System.Threading.Tasks.Parallel.ForEachAsync(
            list.Select((item, index) => (item, index)),
            options,
            async (tuple, ct) =>
            {
                await action(tuple.index, tuple.item, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    public static async Task<IList<TResult>> Parallel<T, TResult>(this IList<T> list, int degree, Func<int, T, CancellationToken, Task<TResult>> action, CancellationToken ctk = default)
    {
        var results = new TResult[list.Count];
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degree,
            CancellationToken = ctk
        };

        await System.Threading.Tasks.Parallel.ForEachAsync(
            list.Select((item, index) => (item, index)),
            options,
            async (tuple, ct) =>
            {
                var result = await action(tuple.index, tuple.item, ct).ConfigureAwait(false);
                results[tuple.index] = result;
            }).ConfigureAwait(false);

        return results;
    }
}