// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
#if NET6_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace Ark.Tools.Core
{
    public static partial class EnumerableExtensions
    {

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
            private static readonly ConcurrentDictionary<string, Func<IQueryable<T>, IQueryable<T>>> _cache = new(StringComparer.Ordinal);

            public static IQueryable<T> ApplyOrderBy(IQueryable<T> collection, string orderBy
#if NET6_0_OR_GREATER
                , [CallerArgumentExpression(nameof(orderBy))] string? orderByParam = null
#endif
            )
            {
#if NET6_0_OR_GREATER
                var paramName = orderByParam;
#else
                var paramName = nameof(orderBy);
#endif

                var apply = _cache.GetOrAdd(orderBy, k =>
                {

                    var chain = _parseOrderBy(k, paramName)
                        .Select(_compileOrderBy).ToArray();

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
                    PropertyInfo? pi = type.GetProperty(prop);
                    if (pi is null) throw new InvalidOperationException($"Property '{prop}' not found in {orderByInfo.PropertyName}");

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

                object? comparer = type == typeof(OffsetDateTime)
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

                IQueryable<T> apply(IQueryable<T> collection) 
                    => (IOrderedQueryable<T>)method.Invoke(null, [collection, lambda, comparer])!
                    ;

                return apply;
            }

            private static IEnumerable<OrderByInfo> _parseOrderBy(string orderBy, string? orderByParam)
            {
                if (String.IsNullOrEmpty(orderBy))
                    yield break;

                string[] items = orderBy.Split(',');
                bool initial = true;
                foreach (string item in items)
                {
                    string[] pair = item.Trim().Split(' ');

                    if (pair.Length > 2)
                        throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "Invalid OrderBy string '{0}'. Order By Format: Property, Property2 ASC, Property2 DESC", item), orderByParam);

                    string prop = pair[0].Trim();

                    if (String.IsNullOrEmpty(prop))
                        throw new ArgumentException("Invalid Property. Order By Format: Property, Property2 ASC, Property2 DESC", orderByParam);

                    SortDirection dir = SortDirection.Ascending;

                    if (pair.Length == 2)
                        dir = ("desc".Equals(pair[1].Trim(), StringComparison.OrdinalIgnoreCase) ? SortDirection.Descending : SortDirection.Ascending);

                    yield return new OrderByInfo(prop, dir, initial);

                    initial = false;
                }
            }

            public record OrderByInfo
            {
                public OrderByInfo(string propertyName, SortDirection direction, bool initial)
                {
                    PropertyName = propertyName;
                    Direction = direction;
                    Initial = initial;
                }

                public string PropertyName { get; init; }
                public SortDirection Direction { get; init; }
                public bool Initial { get; init; }
            }

            public enum SortDirection
            {
                Ascending = 0,
                Descending = 1
            }
        }
    }
}