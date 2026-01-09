using Ark.Reference.Common.Auth;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;

using Dapper;

using FluentValidation;

using Microsoft.Data.SqlClient;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ark.Reference.Common;

public static partial class Ex
{


    public static IEnumerable<Permissions> GetPermissions(this ClaimsPrincipal user)
    {
        var permissionClaim =
            user.FindAll(x => x.Type == PermissionsConstants.PermissionKey).Select(x => x.Value)
            ?? Enumerable.Empty<string>();

        if (permissionClaim.Any(a => a.Contains(PermissionsConstants.AdminGrant, StringComparison.Ordinal)))
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
            .Select(s => Regex.Match(s, "^(?<col>\\S+)(\\s(?<dir>asc|desc))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromMilliseconds(1000)))
            .Where(s => s.Success)
            .Join(validCols
                , s => s.Groups["col"].Value.ToUpperInvariant()
                , d => d.Key.ToUpperInvariant()
                , (s, i) => i.Value + s.Groups["dir"].Value, StringComparer.Ordinal)
            .DefaultIfEmpty(defaultValue)
            .ToArray();
    }

    public static async Task<IEnumerable<TReturn>> QueryAsync<TRead, TReturn>(this IDbConnection cnn, CommandDefinition command, Func<TRead, TReturn> func)
    {
        return (await cnn.QueryAsync<TRead>(command).ConfigureAwait(false))
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
            Data = pagedResult.Data.Select(func).ToList()
        };
    }

    public static IRuleBuilderOptions<T, TElement> OneOf<T, TElement>(this IRuleBuilder<T, TElement> ruleBuilder, params TElement[] choices)
    {
        return ruleBuilder.Must(p => choices.Contains(p)).WithMessage("{PropertyName} must be one of " + string.Join(",", choices));
    }

    public static decimal Round(this decimal value, int roundDecimals)
    {
        return Math.Round(value, roundDecimals, MidpointRounding.AwayFromZero);
    }

    public static decimal? Round(this decimal? value, int roundDecimals)
    {
        if (value.HasValue)
            return Math.Round(value.Value, roundDecimals, MidpointRounding.AwayFromZero);
        else
            return null;
    }

    /// <summary>
    /// Check if Exception is 'Final', thus not to be retried under any circumstances.
    /// </summary>
    /// <param name="exception"></param>
    /// <returns>true if Final, false otherwise</returns>
    public static bool IsFinal(this Exception exception)
    {
        Exception? ex = exception;
        while (ex != null)
        {
            if (ex is ValidationException
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
    /// <param name="exception"></param>
    /// <returns>true if Optimistic, false otherwise</returns>
    public static bool IsOptimistic(this Exception exception)
    {
        Exception? ex = exception;
        while (ex != null)
        {
            if (ex is SqlException sex && sex.Number == 13535 /* Data modification failed on system-versioned... */)
                return true;
            if (ex is OptimisticConcurrencyException)
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