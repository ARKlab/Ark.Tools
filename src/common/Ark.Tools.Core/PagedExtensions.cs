// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    public static class PagedExtensions
    {

        public static async Task<(IEnumerable<TResult>, long count)> ReadAllPagesAsync<TResult, TQuery>(this TQuery query, Func<TQuery, CancellationToken, Task<PagedResult<TResult>>> funcAsync, CancellationToken ctk = default)
        where TQuery : IQueryPaged
        {
            var data = await QueryAllPagesAsync(query, funcAsync, ctk).ToListAsync(ctk).ConfigureAwait(false);
            return (data, data.Count + query.Skip);
        }

        public static async Task<(IEnumerable<TResult>, long count)> ReadAllEnumerableAsync<TResult, TQuery>(
              this TQuery query
            , Func<TQuery, CancellationToken, Task<(IEnumerable<TResult> Data, long Count)>> funcAsync
            , CancellationToken ctk = default)
            where TQuery : IQueryPaged
        {
            var data = await QueryAllPagesAsync(query, async (q, ctk) =>
            {
                var res = await funcAsync(q, ctk).ConfigureAwait(false);

                return new PagedResult<TResult>
                {
                    Count = res.Count,
                    Data = res.Data,
                    IsCountPartial = false,
                    Limit = query.Limit,
                    Skip = query.Skip,
                };
            }, ctk).ToListAsync(ctk).ConfigureAwait(false);

            return (data, data.Count + query.Skip);
        }

        public static async IAsyncEnumerable<TResult> QueryAllPagesAsync<TQuery, TResult>(this TQuery query,
                                                                                          Func<TQuery, CancellationToken, Task<PagedResult<TResult>>> executor,
                                                                                          [EnumeratorCancellation] CancellationToken ctk = default)
            where TQuery : IQueryPaged
        {
            PagedResult<TResult> lastPage;
            do
            {
                lastPage = await executor(query, ctk).ConfigureAwait(false);
                foreach (var e in lastPage.Data)
                    yield return e;
                query.Skip = lastPage.Skip + lastPage.Limit;
            } while (lastPage.Count > query.Skip || (lastPage.IsCountPartial == true && lastPage.Count == query.Skip));
        }
=======
namespace Ark.Tools.Core;

public static class PagedExtensions
{

    public static async Task<(IEnumerable<TResult>, long count)> ReadAllPagesAsync<TResult, TQuery>(this TQuery query, Func<TQuery, CancellationToken, Task<PagedResult<TResult>>> funcAsync, CancellationToken ctk = default)
    where TQuery : IQueryPaged
    {
        var data = await QueryAllPagesAsync(query, funcAsync, ctk).ToListAsync(ctk).ConfigureAwait(false);
        return (data, data.Count + query.Skip);
    }

    public static async Task<(IEnumerable<TResult>, long count)> ReadAllEnumerableAsync<TResult, TQuery>(
          this TQuery query
        , Func<TQuery, CancellationToken, Task<(IEnumerable<TResult> Data, long Count)>> funcAsync
        , CancellationToken ctk = default)
        where TQuery : IQueryPaged
    {
        var data = await QueryAllPagesAsync(query, async (q, ctk) =>
        {
            var res = await funcAsync(q, ctk).ConfigureAwait(false);

            return new PagedResult<TResult>
            {
                Count = res.Count,
                Data = res.Data,
                IsCountPartial = false,
                Limit = query.Limit,
                Skip = query.Skip,
            };
        }, ctk).ToListAsync(ctk).ConfigureAwait(false);

        return (data, data.Count + query.Skip);
    }

    public static async IAsyncEnumerable<TResult> QueryAllPagesAsync<TQuery, TResult>(this TQuery query,
                                                                                      Func<TQuery, CancellationToken, Task<PagedResult<TResult>>> executor,
                                                                                      [EnumeratorCancellation] CancellationToken ctk = default)
        where TQuery : IQueryPaged
    {
        PagedResult<TResult> lastPage;
        do
        {
            lastPage = await executor(query, ctk).ConfigureAwait(false);
            foreach (var e in lastPage.Data)
                yield return e;
            query.Skip = lastPage.Skip + lastPage.Limit;
        } while (lastPage.Count > query.Skip || (lastPage.IsCountPartial == true && lastPage.Count == query.Skip));
>>>>>>> After


namespace Ark.Tools.Core;

public static class PagedExtensions
{

    public static async Task<(IEnumerable<TResult>, long count)> ReadAllPagesAsync<TResult, TQuery>(this TQuery query, Func<TQuery, CancellationToken, Task<PagedResult<TResult>>> funcAsync, CancellationToken ctk = default)
    where TQuery : IQueryPaged
    {
        var data = await QueryAllPagesAsync(query, funcAsync, ctk).ToListAsync(ctk).ConfigureAwait(false);
        return (data, data.Count + query.Skip);
    }

    public static async Task<(IEnumerable<TResult>, long count)> ReadAllEnumerableAsync<TResult, TQuery>(
          this TQuery query
        , Func<TQuery, CancellationToken, Task<(IEnumerable<TResult> Data, long Count)>> funcAsync
        , CancellationToken ctk = default)
        where TQuery : IQueryPaged
    {
        var data = await QueryAllPagesAsync(query, async (q, ctk) =>
        {
            var res = await funcAsync(q, ctk).ConfigureAwait(false);

            return new PagedResult<TResult>
            {
                Count = res.Count,
                Data = res.Data,
                IsCountPartial = false,
                Limit = query.Limit,
                Skip = query.Skip,
            };
        }, ctk).ToListAsync(ctk).ConfigureAwait(false);

        return (data, data.Count + query.Skip);
    }

    public static async IAsyncEnumerable<TResult> QueryAllPagesAsync<TQuery, TResult>(this TQuery query,
                                                                                      Func<TQuery, CancellationToken, Task<PagedResult<TResult>>> executor,
                                                                                      [EnumeratorCancellation] CancellationToken ctk = default)
        where TQuery : IQueryPaged
    {
        PagedResult<TResult> lastPage;
        do
        {
            lastPage = await executor(query, ctk).ConfigureAwait(false);
            foreach (var e in lastPage.Data)
                yield return e;
            query.Skip = lastPage.Skip + lastPage.Limit;
        } while (lastPage.Count > query.Skip || (lastPage.IsCountPartial == true && lastPage.Count == query.Skip));
    }
}