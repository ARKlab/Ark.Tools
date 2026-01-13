// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;

using NodaTime;

using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ark.Tools.Core.Reflection;

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
            , [CallerArgumentExpression(nameof(orderBy))] string? orderByParam = null
        )
        {
            var paramName = orderByParam;

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

            // Parse all items first to avoid span lifetime issues with yield
            var items = _parseOrderByItems(orderBy, orderByParam);
            
            foreach (var item in items)
            {
                yield return item;
            }
        }

        private static List<OrderByInfo> _parseOrderByItems(string orderBy, string? orderByParam)
        {
            var result = new List<OrderByInfo>();
            
            // Use span to avoid string allocations during parsing
            ReadOnlySpan<char> span = orderBy.AsSpan();
            bool initial = true;
            
            // Process each comma-separated item
            while (span.Length > 0)
            {
                // Find the next comma
                int commaIndex = span.IndexOf(',');
                ReadOnlySpan<char> item;
                
                if (commaIndex >= 0)
                {
                    item = span[..commaIndex].Trim();
                    span = span[(commaIndex + 1)..];
                }
                else
                {
                    item = span.Trim();
                    span = ReadOnlySpan<char>.Empty;
                }

                // Parse the property and direction from this item
                // Split on space to separate property name from ASC/DESC
                int spaceIndex = item.IndexOf(' ');
                ReadOnlySpan<char> propertySpan;
                ReadOnlySpan<char> directionSpan = ReadOnlySpan<char>.Empty;
                int partCount = 1;

                if (spaceIndex >= 0)
                {
                    propertySpan = item[..spaceIndex].Trim();
                    var remainder = item[(spaceIndex + 1)..].Trim();
                    
                    if (remainder.Length > 0)
                    {
                        directionSpan = remainder;
                        partCount = 2;
                        
                        // Check if there are more than 2 parts (space-separated tokens)
                        int secondSpaceIndex = directionSpan.IndexOf(' ');
                        if (secondSpaceIndex >= 0)
                        {
                            partCount = 3; // At least 3 parts detected
                        }
                    }
                }
                else
                {
                    propertySpan = item;
                }

                if (partCount > 2)
                    throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "Invalid OrderBy string '{0}'. Order By Format: Property, Property2 ASC, Property2 DESC", item.ToString()), orderByParam);

                if (propertySpan.IsEmpty)
                    throw new ArgumentException("Invalid Property. Order By Format: Property, Property2 ASC, Property2 DESC", orderByParam);

                // Convert property span to string (required for the OrderByInfo)
                string prop = propertySpan.ToString();

                SortDirection dir = SortDirection.Ascending;

                if (partCount == 2)
                    dir = (directionSpan.Equals("desc", StringComparison.OrdinalIgnoreCase) ? SortDirection.Descending : SortDirection.Ascending);

                result.Add(new OrderByInfo(prop, dir, initial));

                initial = false;
            }
            
            return result;
        }

        public sealed record OrderByInfo
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