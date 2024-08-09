using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;

using Dapper;

using Ark.Reference.Common.Auth;
using Ark.Reference.Common.Dto;

using FluentValidation;

using Microsoft.Data.SqlClient;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common
{
    public static partial class Ex
    {


        public static IEnumerable<Permissions> GetPermissions(this ClaimsPrincipal user)
        {
            var permissionClaim =
                user.FindAll(x => x.Type == PermissionsConstants.PermissionKey).Select(x => x.Value)
                ?? Enumerable.Empty<string>();

            if (permissionClaim.Any(a => a.Contains(PermissionsConstants.AdminGrant)))
                return PermissionsConstants.PermissionsMap.Values;
            else
                return permissionClaim.Select(x => PermissionsConstants.PermissionsMap[x]);
        }

        //***************************************************************************************************************
        //** PAGED EXTENSIONS
        //***************************************************************************************************************

        public static string[] CompileSorts(this IEnumerable<string> sorts, Dictionary<string, string> validCols, string defaultValue)
        {
            return (sorts ?? Enumerable.Empty<string>())
                .Select(s => Regex.Match(s, "^(\\S+)(\\s(asc|desc))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .Where(s => s.Success)
                .Join(validCols
                    , s => s.Groups[1].Value.ToLowerInvariant()
                    , d => d.Key.ToLowerInvariant()
                    , (s, i) => i.Value + s.Groups[2].Value)
                .DefaultIfEmpty(defaultValue)
                .ToArray();
        }

        public static async Task<IEnumerable<TReturn>> QueryAsync<TRead, TReturn>(this IDbConnection cnn, CommandDefinition command, Func<TRead, TReturn> func)
        {
            return (await cnn.QueryAsync<TRead>(command))
                .Select(s => func(s))
                .ToArray();
        }

        public static PagedResult<TReturn> Convert<TRead, TReturn>(this PagedResult<TRead> pagedResult, Func<TRead, TReturn> func)
        {
            return new PagedResult<TReturn>()
            {
                Count = pagedResult.Count,
                IsCountPartial = pagedResult.IsCountPartial,
                Limit = pagedResult.Limit,
                Skip = pagedResult.Skip,
                Data = pagedResult.Data?
                                  .Select(s => func(s))
                                  .ToArray()
            };
        }


        public static async Task<(IEnumerable<TResult>, int count)> ReadAllPagesAsync<TResult, TQuery>(this TQuery query, Func<TQuery, CancellationToken, Task<PagedResult<TResult>>> funcAsync, CancellationToken ctk = default)
        where TQuery : IQueryPaged
        {
            return await ReadAllEnumerableAsync<TResult, TQuery>(query, async (q, c) =>
            {
                var a = await funcAsync(q, c);
                var retVal = (a.Data, (int)a.Count);
                return retVal;
            }, ctk);
        }

        public static async Task<(IEnumerable<TResult>, int count)> ReadAllEnumerableAsync<TResult, TQuery>(
              this TQuery query
            , Func<TQuery, CancellationToken, Task<(IEnumerable<TResult> Data, int Count)>> funcAsync
            , CancellationToken ctk = default)
        where TQuery : IQueryPaged
        {
            var skip = 0;
            var limit = 1000;

            var count = 0;
            var retVal = new List<TResult>();

            while (count > skip || count == 0)
            {
                query.Limit = limit;
                query.Skip = skip;

                var res = await funcAsync(query, ctk);

                if (res.Count > 0)
                {
                    count = (int)res.Count;
                    skip += limit;
                    retVal.AddRange(res.Data);
                }
                else
                {
                    break;
                }
            }

            return (retVal, count);
        }

        public static async IAsyncEnumerable<TResult> QueryAllPagesAsync<TQuery, TResult>(this TQuery query,
                                                                                          Func<TQuery, CancellationToken, Task<PagedResult<TResult>>> executor,
                                                                                          [EnumeratorCancellation] CancellationToken ctk = default)
            where TQuery : IQueryPaged
        {
            PagedResult<TResult> page;
            do
            {
                page = await executor(query, ctk);
                foreach (var e in page.Data)
                    yield return e;
                query.Skip += query.Limit;
            } while (page.Count > query.Skip);
        }


        public static IRuleBuilderOptions<T, TElement> OneOf<T, TElement>(this IRuleBuilder<T, TElement> ruleBuilder, params TElement[] choices)
        {
            return ruleBuilder.Must(p => choices.Contains(p)).WithMessage("{PropertyName} must be one of " + string.Join(",", choices));
        }

        public static decimal Round(this decimal value, int roundDecimals)
        {
            return Math.Round(value, roundDecimals);
        }

        public static decimal? Round(this decimal? value, int roundDecimals)
        {
            if (value.HasValue)
                return Math.Round(value.Value, roundDecimals);
            else
                return null;
        }

        /// <summary>
        /// Check if Exception is 'Final', thus not to be retried under any circumstances.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns>true if Final, false otherwise</returns>
        public static bool IsFinal(this Exception ex)
        {
            while (ex != null)
            {
                if (ex is FluentValidation.ValidationException
                  || ex is BusinessRuleViolationException
                  || ex is NotImplementedException
                  || ex is NotSupportedException
                  )
                    return true;

                ex = ex.InnerException;
            }
            return false;
        }

        /// <summary>
        /// Check if Exception is 'Optimistic', thus can be retried immediatly
        /// </summary>
        /// <param name="ex"></param>
        /// <returns>true if Optimistic, false otherwise</returns>
        public static bool IsOptimistic(this Exception ex)
        {
            while (ex != null)
            {
                if (ex is SqlException sex && sex.Number == 13535 /* Data modification failed on system-versioned... */)
                    return true;
                if (ex is Ark.Tools.Core.OptimisticConcurrencyException)
                    return true;

                ex = ex.InnerException;
            }
            return false;
        }


        public static OffsetDateTime StartOfDayInZone(this OffsetDateTime odt, int plusDays, DateTimeZone tz)
        {
            var p = Period.FromDays(plusDays);
            return odt.InZone(tz).Date.Plus(p).AtStartOfDayInZone(tz).ToOffsetDateTime();
        }
    }

    


}