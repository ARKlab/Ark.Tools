using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Nodatime;
using Ark.Tools.Nodatime.SystemTextJson;
using Ark.Tools.Solid;
using Ark.Tools.SystemTextJson;

using Azure.Identity;

using Dapper;

using Ark.Reference.Common.Auth;
using Ark.Reference.Common.Dto;

using FluentValidation;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

using Rebus.Bus;
using Rebus.Pipeline;
using Rebus.Retry.Simple;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common
{
    public static partial class Ex
    {
        //
        // Summary:
        //     Read the next grid of results.
        //
        // Parameters:
        //   buffered:
        //     Whether the results should be buffered in memory.
        //
        // Type parameters:
        //   TRead:
        //     The type to read.
        //   TReturn:
        //     The type to return from the record set.

        public static JsonSerializerOptions ConfigureArkDefaultsNoCamelKey(this JsonSerializerOptions @this)
        {
            @this.AllowTrailingCommas = true;
            @this.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            @this.DictionaryKeyPolicy = null;
            @this.PropertyNameCaseInsensitive = true;

            //@this.Converters.Insert(0, new NullableStructSerializerFactory()); // not required anymore in v5
            @this.Converters.Add(new JsonStringEnumMemberConverter()); // from macross
            @this.Converters.Add(new GenericDictionaryWithConvertibleKey());

            @this.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            @this.ConfigureForNodaTimeRanges();

            @this.Converters.Add(new JsonIPAddressConverter());
            @this.Converters.Add(new JsonIPEndPointConverter());

            @this.Converters.Add(new UniversalInvariantTypeConverterJsonConverter()); // as last resort

            return @this;
        }

        public static string AsString<T>(this T value)
                where T : System.Enum
        {
            DescriptionAttribute desc = typeof(T)
                .GetField(value.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .SingleOrDefault() as DescriptionAttribute;


            EnumMemberAttribute em = typeof(T)
                .GetField(value.ToString())
                .GetCustomAttributes(typeof(EnumMemberAttribute), false)
                .SingleOrDefault() as EnumMemberAttribute;

            return em?.Value ?? desc?.Description ?? value.ToString();
        }
        public static List<T> ReplaceCollectionElement<T>(this List<T> collection,T oldValue, T newValue)
        {
            var updatedCollection = collection.ToList();

            var index = collection.IndexOf(oldValue);

            updatedCollection[index] = newValue;

            return updatedCollection;
        }

        //***************************************************************************************************************
        //** USER ID EXTENSIONS
        //***************************************************************************************************************
        public static string GetB2CUserMail(this ClaimsPrincipal principal)
        {
            var userId = principal?.Claims?.FirstOrDefault(c => c.Type == "emails");

            var u = userId?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value; //This second part is for userId

            return u;
        }

        public static string GetB2CUserMail(this IContextProvider<ClaimsPrincipal> userContext)
        {
            return userContext.Current?.GetB2CUserMail();
        }

        public static IEnumerable<Permissions> GetAdminPermissions(this ClaimsPrincipal user)
        {
            var permissionClaim =
                user.FindAll(x => x.Type == PermissionsConstants.PermissionKey).Select(x => x.Value)
                ?? Enumerable.Empty<string>();

            if(permissionClaim.Any(a => a.Contains(PermissionsConstants.AdminGrant)))
                return permissionClaim.Select(x => PermissionsConstants.PermissionsMap[x]);
            else 
                return Enumerable.Empty<Permissions>();
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
                    , s => s.Groups[1].Value.ToLower()
                    , d => d.Key.ToLower()
                    , (s, i) => i.Value + s.Groups[2].Value)
                .DefaultIfEmpty(defaultValue)
                .ToArray();
        }

        public static string ConvertToPaged(this string query, string[] sortFields)
        {
            return $@"
                {query}

                ORDER BY {String.Join(", ", sortFields)}
                OFFSET @Skip ROWS FETCH NEXT @Limit ROWS ONLY

                SELECT COUNT(*) FROM({query}) a";
        }

        public static async Task<IEnumerable<TReturn>> ReadAsync<TRead, TReturn>(this SqlMapper.GridReader g, Func<TRead, TReturn> func, bool buffered = true)
        {
            return (await g.ReadAsync<TRead>(buffered))
                .Select(s => func(s))
                .ToArray();
        }

        public static async Task<(IEnumerable<TReturn> data, int count)> ReadPagedAsync<TReturn>(this IDbConnection connection, CommandDefinition cmd)
        {
            using var r = await connection.QueryMultipleAsync(cmd);
            var retVal = await r.ReadAsync<TReturn>();
            var count = await r.ReadFirstAsync<int>();

            return (retVal, count);
        }

        //***************************************************************************************************************
        //** SERIALIZE JSON EXTENSIONS (SYSTEM TEXT JSON)
        //***************************************************************************************************************
        public static byte[] Serialize(this object obj, JsonSerializerOptions jsonSerializerOptions = null)
        {
            if (obj == null)
                return null;
            return JsonSerializer.SerializeToUtf8Bytes(obj, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
        }

        public static TOut Deserialize<TOut>(this byte[] bytes, JsonSerializerOptions jsonSerializerOptions = null)
        {
            if (bytes == null)
                return default;

            var span = new Span<byte>(bytes);
            if (span.StartsWith(Encoding.UTF8.Preamble)) // UTF8 BOM
                span = span.Slice(Encoding.UTF8.Preamble.Length);

            return JsonSerializer.Deserialize<TOut>(span, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
        }

        public static object Deserialize(this byte[] bytes, Type type)
        {
            if (bytes == null)
                return default;

            return JsonSerializer.Deserialize(bytes, type, ArkSerializerOptions.JsonOptions);
        }

        public static string ToJsonString(this object obj)
        {
            if (obj == null)
                return null;

            return JsonSerializer.Serialize(obj, ArkSerializerOptions.JsonOptions);
        }

        public static TOut FromJsonStringToObj<TOut>(this string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return default;

            return JsonSerializer.Deserialize<TOut>(jsonString, ArkSerializerOptions.JsonOptions);
        }

        public static TOut FromJsonStringFileToObj<TOut>(this string jsonStringFilePath)
        {
            if (string.IsNullOrEmpty(jsonStringFilePath))
                return default;

            using (StreamReader r = new StreamReader(jsonStringFilePath))
            {
                string jsonString = r.ReadToEnd();
                return jsonString.FromJsonStringToObj<TOut>();
            }
        }

        public static object FromJsonStringToObj(this string jsonString, Type type)
        {
            if (string.IsNullOrEmpty(jsonString))
                return default;

            return JsonSerializer.Deserialize(jsonString, type, ArkSerializerOptions.JsonOptions);
        }

        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        //
        // Summary:
        //     Execute a query asynchronously using Task.
        //
        // Parameters:
        //   cnn:
        //     The connection to query on.
        //
        //   command:
        //     The command used to query on this connection.
        //
        // Type parameters:
        //   TRead:
        //     The type to read.
        //   TReturn:
        //     The type to return from the record set.
        //
        // Returns:
        //     A sequence of data of T; if a basic type (int, string, etc) is queried then the
        //     data from the first column in assumed, otherwise an instance is created per row,
        //     and a direct column-name===member-name mapping is assumed (case insensitive).
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

        //public static Task RetryDeferred<T>(this IBus bus, IFailed<T> message, int maxRetries, TimeSpan delay, Func<Task> onRetryFailure = null)
        //{
        //    int cnt = 0;
        //    if (!message.Headers.TryGetValue("defer-retry", out var count)
        //        || !int.TryParse(count, out cnt)
        //        || cnt < maxRetries)
        //    {
        //        return bus.Defer(delay, message.Message, new Dictionary<string, string>
        //        {
        //            { "defer-retry", (++cnt).ToString() }
        //        });
        //    }

        //    return onRetryFailure?.Invoke() ?? Task.FromException(new ApplicationException("RetryDeferred: Too many retry", message.Exceptions.FirstOrDefault()));
        //}

        public static Task RetryDeferred(this IBus bus, int maxRetries, TimeSpan delay, Action onRetryFailure = null)
        {
            int cnt = 0;
            var currentContext = MessageContext.Current;
            if (!currentContext.Headers.TryGetValue("defer-retry", out var count)
                || !int.TryParse(count, out cnt)
                || cnt < maxRetries)
                return bus.Defer(delay, new Dictionary<string, string>
                {
                    { "defer-retry", (++cnt).ToString() }
                });

            onRetryFailure?.Invoke();

            return Task.CompletedTask;
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

        public static IEnumerable<TResult> FullOuterGroupJoin<TA, TB, TKey, TResult>(this IEnumerable<TA> a,
                                                                                     IEnumerable<TB> b,
                                                                                     Func<TA, TKey> selectKeyA,
                                                                                     Func<TB, TKey> selectKeyB,
                                                                                     Func<IEnumerable<TA>, IEnumerable<TB>, TKey, TResult> projection,
                                                                                     IEqualityComparer<TKey> cmp = null)
        {
            cmp ??= EqualityComparer<TKey>.Default;
            var alookup = a.ToLookup(selectKeyA, cmp);
            var blookup = b.ToLookup(selectKeyB, cmp);

            var keys = new HashSet<TKey>(alookup.Select(p => p.Key), cmp);
            keys.UnionWith(blookup.Select(p => p.Key));

            var join = from key in keys
                       let xa = alookup[key]
                       let xb = blookup[key]
                       select projection(xa, xb, key);

            return join;
        }

        public static IEnumerable<TResult> FullOuterJoin<TA, TB, TKey, TResult>(
            this IEnumerable<TA> a,
            IEnumerable<TB> b,
            Func<TA, TKey> selectKeyA,
            Func<TB, TKey> selectKeyB,
            Func<TA, TB, TKey, TResult> projection,
            TA defaultA = default(TA),
            TB defaultB = default(TB),
            IEqualityComparer<TKey> cmp = null)
        {
            cmp ??= EqualityComparer<TKey>.Default;
            var alookup = a.ToLookup(selectKeyA, cmp);
            var blookup = b.ToLookup(selectKeyB, cmp);

            var keys = new HashSet<TKey>(alookup.Select(p => p.Key), cmp);
            keys.UnionWith(blookup.Select(p => p.Key));

            var join = from key in keys
                       from xa in alookup[key].DefaultIfEmpty(defaultA)
                       from xb in blookup[key].DefaultIfEmpty(defaultB)
                       select projection(xa, xb, key);

            return join;
        }

        public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> enumerable, string orderBy)
        {
            return enumerable.AsQueryable().OrderBy(orderBy).AsEnumerable();
        }

        public static IQueryable<T> OrderBy<T>(this IQueryable<T> collection, string orderBy)
        {
            return QueryCompiler<T>.ApplyOrderBy(collection, orderBy);
        }

        static class QueryCompiler<T>
        {
            private static ConcurrentDictionary<string, Func<IQueryable<T>, IQueryable<T>>> _cache = new();

            public static IQueryable<T> ApplyOrderBy(IQueryable<T> collection, string orderBy)
            {
                var apply = _cache.GetOrAdd(orderBy, k =>
                {
                    var chain = _parseOrderBy(k).Select(_compileOrderBy).ToArray();

                    IQueryable<T> apply(IQueryable<T> c)
                    {
                        foreach (var item in chain)
                            c = item(c);

                        return c;
                    }

                    return apply;
                });

                return apply(collection);
            }


            private static Func<IQueryable<T>, IQueryable<T>> _compileOrderBy(OrderByInfo orderByInfo)
            {
                string[] props = orderByInfo.PropertyName.Split('.');
                Type type = typeof(T);

                ParameterExpression arg = Expression.Parameter(type, "x");
                Expression expr = arg;
                foreach (string prop in props)
                {
                    // use reflection (not ComponentModel) to mirror LINQ
                    PropertyInfo pi = type.GetProperty(prop);
                    expr = Expression.Property(expr, pi);
                    type = pi.PropertyType;
                }
                Type delegateType = typeof(Func<,>).MakeGenericType(typeof(T), type);
                var lambda = Expression.Lambda(delegateType, expr, arg);
                string methodName = String.Empty;

                if (orderByInfo.Initial)
                {
                    if (orderByInfo.Direction == SortDirection.Ascending)
                        methodName = "OrderBy";
                    else
                        methodName = "OrderByDescending";
                }
                else
                {
                    if (orderByInfo.Direction == SortDirection.Ascending)
                        methodName = "ThenBy";
                    else
                        methodName = "ThenByDescending";
                }

                object comparer = type == typeof(OffsetDateTime)
                    ? OffsetDateTime.Comparer.Instant
                    : type == typeof(OffsetDateTime?)
                        ? new OffsetDateTimeNullableComparer()
                        : null;

                var method = typeof(Queryable).GetMethods().Single(
                    method => method.Name == methodName
                            && method.IsGenericMethodDefinition
                            && method.GetGenericArguments().Length == 2
                            && method.GetParameters().Length == 3)
                    .MakeGenericMethod(typeof(T), type)
                    ;

                IQueryable<T> apply(IQueryable<T> collection) => (IOrderedQueryable<T>)method
                    .Invoke(null, new object[] { collection, lambda, comparer })
                    ;

                return apply;
            }

            private static IEnumerable<OrderByInfo> _parseOrderBy(string orderBy)
            {
                if (String.IsNullOrEmpty(orderBy))
                    yield break;

                string[] items = orderBy.Split(',');
                bool initial = true;
                foreach (string item in items)
                {
                    string[] pair = item.Trim().Split(' ');

                    if (pair.Length > 2)
                        throw new ArgumentException(String.Format("Invalid OrderBy string '{0}'. Order By Format: Property, Property2 ASC, Property2 DESC", item));

                    string prop = pair[0].Trim();

                    if (String.IsNullOrEmpty(prop))
                        throw new ArgumentException("Invalid Property. Order By Format: Property, Property2 ASC, Property2 DESC");

                    SortDirection dir = SortDirection.Ascending;

                    if (pair.Length == 2)
                        dir = ("desc".Equals(pair[1].Trim(), StringComparison.OrdinalIgnoreCase) ? SortDirection.Descending : SortDirection.Ascending);

                    yield return new OrderByInfo() { PropertyName = prop, Direction = dir, Initial = initial };

                    initial = false;
                }
            }

            public record OrderByInfo
            {
                public string PropertyName { get; set; }
                public SortDirection Direction { get; set; }
                public bool Initial { get; set; }
            }

            public enum SortDirection
            {
                Ascending = 0,
                Descending = 1
            }
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

 

        /// <summary>
        /// AddAzureKeyVaultMSI
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IConfigurationBuilder AddAzureKeyVaultMSI(this IConfigurationBuilder builder)
        {
            var config = builder.Build();
            var keyVaultBaseUrl = config["KeyVault:BaseUrl"];

            if (!string.IsNullOrEmpty(keyVaultBaseUrl))
                builder.AddAzureKeyVault(
                    new Uri(keyVaultBaseUrl)
                    , new DefaultAzureCredential()
                    , new ArkKeyVaultSecretManager()
                );

            return builder;
        }

        public static OffsetDateTime StartOfDayInZone(this OffsetDateTime odt, int plusDays, DateTimeZone tz)
        {
            var p = Period.FromDays(plusDays);
            return odt.InZone(tz).Date.Plus(p).AtStartOfDayInZone(tz).ToOffsetDateTime();
        }
    }

    public class OffsetDateTimeNullableComparer : IComparer<OffsetDateTime?>, IEqualityComparer<OffsetDateTime?>
    {
        internal static readonly OffsetDateTime.Comparer _instance = OffsetDateTime.Comparer.Instant;

        public int Compare(OffsetDateTime? x, OffsetDateTime? y)
        {

            //Two nulls are equal
            if (!x.HasValue && !y.HasValue)
                return 0;

            //Any object is different than null
            if (x.HasValue && !y.HasValue)
                return 1;

            if (y.HasValue && !x.HasValue)
                return -1;

            //Otherwise compare the two values
            return _instance.Compare(x.Value, y.Value);
        }

        public bool Equals(OffsetDateTime? x, OffsetDateTime? y)
        {
            //Two nulls are equal
            if (!x.HasValue && !y.HasValue)
                return true;

            //Any object is different than null
            if (x.HasValue && !y.HasValue)
                return false;

            if (y.HasValue && !x.HasValue)
                return false;

            //Otherwise equals the two values
            return _instance.Equals(x.Value, y.Value);
        }

        public int GetHashCode(OffsetDateTime? obj)
        {
            if (obj.HasValue)
                return _instance.GetHashCode(obj.Value);
            else
                return 0;
        }
        

    }

}