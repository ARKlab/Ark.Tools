﻿using Ark.Tools.Core;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.RavenDb
{
    public static class Ex
    {
        public static async Task<PagedResult<T>> GetPagedWithODataOptions<T>(this IRavenQueryable<T> query
                                                                            , ODataQueryOptions<T> options
                                                                            , ODataValidationSettings? validations = null
                                                                            , int defaultPageSize = 100
                                                                            , CancellationToken ctk = default)
                                                                            where T : class
        {
            query.Statistics(out QueryStatistics stats);

            var settings = new ODataQuerySettings
            {
                HandleNullPropagation = HandleNullPropagationOption.False
            };

            //Validations
            options.Validate(validations ?? new RavenDefaultODataValidationSettings());

            //Query
            query = (options.Filter?.ApplyTo(query, settings) as IRavenQueryable<T>) ?? query;
            query = (options.OrderBy?.ApplyTo(query, settings) as IRavenQueryable<T>) ?? query;
            query = query.Skip(options.Skip?.Value ?? 0).Take(options.Top?.Value ?? defaultPageSize);

            var data = await query.ToListAsync(ctk).ConfigureAwait(false);

            return new PagedResult<T>
            {
                Count = stats.TotalResults,
                Data = data,
                IsCountPartial = false,
                Limit = options.Top?.Value ?? defaultPageSize,
                Skip = options.Skip?.Value ?? 0
            };
        }
    }
}
