// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using NodaTime;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class SqlServerNodaTimeAddTranslator : IMethodCallTranslator
    {
        private readonly Dictionary<MethodInfo, string> _methodInfoDatePartMapping = new Dictionary<MethodInfo, string>
        {
            { typeof(LocalDate).GetRuntimeMethod(nameof(LocalDate.PlusYears), new[] { typeof(int) }), "year" },
            { typeof(LocalDate).GetRuntimeMethod(nameof(LocalDate.PlusMonths), new[] { typeof(int) }), "month" },
            { typeof(LocalDate).GetRuntimeMethod(nameof(LocalDate.PlusDays), new[] { typeof(int) }), "day" },
            { typeof(LocalDate).GetRuntimeMethod(nameof(LocalDate.PlusWeeks), new[] { typeof(int) }), "week" },

            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusYears), new[] { typeof(int) }), "year" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusMonths), new[] { typeof(int) }), "month" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusDays), new[] { typeof(int) }), "day" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusWeeks), new[] { typeof(int) }), "week" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusHours), new[] { typeof(long) }), "hour" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusMinutes), new[] { typeof(long) }), "minute" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusSeconds), new[] { typeof(long) }), "second" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusMilliseconds), new[] { typeof(long) }), "millisecond" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.PlusNanoseconds), new[] { typeof(long) }), "nanosecond" },

            { typeof(OffsetDateTime).GetRuntimeMethod(nameof(OffsetDateTime.PlusHours), new[] { typeof(int) }), "hour" },
            { typeof(OffsetDateTime).GetRuntimeMethod(nameof(OffsetDateTime.PlusMinutes), new[] { typeof(int) }), "minute" },
            { typeof(OffsetDateTime).GetRuntimeMethod(nameof(OffsetDateTime.PlusSeconds), new[] { typeof(long) }), "second" },
            { typeof(OffsetDateTime).GetRuntimeMethod(nameof(OffsetDateTime.PlusMilliseconds), new[] { typeof(long) }), "millisecond" },
            { typeof(OffsetDateTime).GetRuntimeMethod(nameof(OffsetDateTime.PlusNanoseconds), new[] { typeof(long) }), "nanosecond" }
        };

        /// <summary>
        ///     Translates the given method call expression.
        /// </summary>
        /// <param name="methodCallExpression">The method call expression.</param>
        /// <returns>
        ///     A SQL expression representing the translated MethodCallExpression.
        /// </returns>
        public virtual Expression Translate(
            MethodCallExpression methodCallExpression)
        {
            if (_methodInfoDatePartMapping.TryGetValue(methodCallExpression.Method, out var datePart))
            {
                var amountToAdd = methodCallExpression.Arguments.First();

                if (amountToAdd is ConstantExpression constantExpression && constantExpression.Value is long
                       && ((long)constantExpression.Value >= int.MaxValue
                           || (long)constantExpression.Value <= int.MinValue))
                    return null;

                return new SqlFunctionExpression(
                        functionName: "DATEADD",
                        returnType: methodCallExpression.Type,
                        arguments: new[] { new SqlFragmentExpression(datePart), amountToAdd, methodCallExpression.Object });
            }

            return null;
        }
    }
}
