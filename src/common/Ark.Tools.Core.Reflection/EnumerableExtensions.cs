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
                // Parse and compile in one pass to avoid intermediate allocations
                var chain = _parseAndCompileOrderBy(k, paramName);

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


        private static Func<IQueryable<T>, IQueryable<T>>[] _parseAndCompileOrderBy(string orderBy, string? orderByParam)
        {
            var result = new List<Func<IQueryable<T>, IQueryable<T>>>();
            
            // Use modern MemoryExtensions.Split to avoid allocations
            ReadOnlySpan<char> span = orderBy.AsSpan();
            bool initial = true;
            
            // Process each comma-separated item using SpanSplitEnumerator
            foreach (var itemRange in span.Split(','))
            {
                ReadOnlySpan<char> item = span[itemRange].Trim();
                
                if (item.IsEmpty)
                    continue;

                // Split on space to separate property name from ASC/DESC
                var spaceEnumerator = item.SplitAny(" ");
                
                if (!spaceEnumerator.MoveNext())
                {
                    throw new ArgumentException("Invalid Property. Order By Format: Property, Property2 ASC, Property2 DESC", orderByParam);
                }
                
                ReadOnlySpan<char> propertySpan = item[spaceEnumerator.Current].Trim();
                
                if (propertySpan.IsEmpty)
                    throw new ArgumentException("Invalid Property. Order By Format: Property, Property2 ASC, Property2 DESC", orderByParam);

                SortDirection dir = SortDirection.Ascending;
                
                // Check if there's a second part (ASC/DESC)
                if (spaceEnumerator.MoveNext())
                {
                    ReadOnlySpan<char> directionSpan = item[spaceEnumerator.Current].Trim();
                    
                    // Check if there are more than 2 parts
                    if (spaceEnumerator.MoveNext())
                    {
                        throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "Invalid OrderBy string '{0}'. Order By Format: Property, Property2 ASC, Property2 DESC", item.ToString()), orderByParam);
                    }
                    
                    if (!directionSpan.IsEmpty)
                    {
                        dir = directionSpan.Equals("desc", StringComparison.OrdinalIgnoreCase) 
                            ? SortDirection.Descending 
                            : SortDirection.Ascending;
                    }
                }

                // Compile directly without creating intermediate struct
                result.Add(_compileOrderBy(propertySpan, dir, initial));

                initial = false;
            }
            
            return result.ToArray();
        }

        private static Func<IQueryable<T>, IQueryable<T>> _compileOrderBy(ReadOnlySpan<char> propertyPath, SortDirection direction, bool initial)
        {
            Type type = typeof(T);

            ParameterExpression arg = Expression.Parameter(type, "x");
            Expression expr = arg;
            
            // Split property path by '.' and navigate the property chain
            foreach (var propRange in propertyPath.Split('.'))
            {
                ReadOnlySpan<char> propSpan = propertyPath[propRange];
                
                // use reflection (not ComponentModel) to mirror LINQ
                PropertyInfo? pi = type.GetProperty(propSpan.ToString());
                if (pi is null) 
                    throw new InvalidOperationException($"Property '{propSpan.ToString()}' not found in {propertyPath.ToString()}");

                expr = Expression.Property(expr, pi);
                type = pi.PropertyType;
            }
            
            Type delegateType = typeof(Func<,>).MakeGenericType(typeof(T), type);
            var lambda = Expression.Lambda(delegateType, expr, arg);
            string methodName = String.Empty;

            if (initial)
            {
                if (direction == SortDirection.Ascending)
                    methodName = "OrderBy";
                else
                    methodName = "OrderByDescending";
            }
            else
            {
                if (direction == SortDirection.Ascending)
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

        public enum SortDirection
        {
            Ascending = 0,
            Descending = 1
        }
    }
}