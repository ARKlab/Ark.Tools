using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{

    public class SqlServerNodaTimeToSystemTypeTranslator : IMethodCallTranslator
    {

        private readonly Dictionary<MethodInfo, string> _methodConversionMapping = new Dictionary<MethodInfo, string>
        {
            { typeof(LocalDate).GetRuntimeMethod(nameof(LocalDate.ToDateTimeUnspecified), new Type[]{ }), "datetime2" },
            { typeof(LocalDate).GetRuntimeMethod(nameof(LocalDate.AtMidnight), new Type[]{ }), "datetime2" },
            { typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.ToDateTimeUnspecified), new Type[]{ }), "datetime2" },
            { typeof(Instant).GetRuntimeMethod(nameof(Instant.ToDateTimeUtc), new Type[]{ }), "datetime2" },
            { typeof(OffsetDateTime).GetRuntimeMethod(nameof(OffsetDateTime.ToDateTimeOffset), new Type[]{ }), "datetimeoffset" },
        };

        private readonly MethodInfo _instantToDateTimeOffset = typeof(Instant).GetRuntimeMethod(nameof(Instant.ToDateTimeOffset), new Type[] { });

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
            if (_methodConversionMapping.TryGetValue(methodCallExpression.Method, out var targetType))
            {
                return new SqlFunctionExpression(
                        functionName: "CONVERT",
                        returnType: methodCallExpression.Type,
                        arguments: new[] { new SqlFragmentExpression(targetType), methodCallExpression.Object });
            }

            if (_instantToDateTimeOffset == methodCallExpression.Method)            
                return new SqlFunctionExpression(
                        "TODATETIMEOFFSET",
                        methodCallExpression.Type,
                        new[] { methodCallExpression.Object, new SqlFragmentExpression("0") });
            

            return null;
        }
    }
}
