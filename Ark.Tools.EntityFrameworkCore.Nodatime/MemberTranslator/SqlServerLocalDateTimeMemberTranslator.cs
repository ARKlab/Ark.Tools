// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using NodaTime;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class SqlServerLocalDateTimeMemberTranslator : IMemberTranslator
    {
        private static readonly Dictionary<string, string> _datePartMapping
            = new Dictionary<string, string>
            {
                { nameof(LocalDateTime.Year), "year" },
                { nameof(LocalDateTime.Month), "month" },
                { nameof(LocalDateTime.DayOfYear), "dayofyear" },
                { nameof(LocalDateTime.Day), "day" },

                { nameof(LocalDateTime.Hour), "hour" },
                { nameof(LocalDateTime.Minute), "minute" },
                { nameof(LocalDateTime.Second), "second" },
                { nameof(LocalDateTime.Millisecond), "millisecond" },
                { nameof(LocalDateTime.NanosecondOfSecond), "nanosecond" }
            };

        public Expression Translate(MemberExpression memberExpression)
        {
            var declaringType = memberExpression.Member.DeclaringType;
            if (declaringType == typeof(LocalDateTime))
            {
                var memberName = memberExpression.Member.Name;

                if (_datePartMapping.TryGetValue(memberName, out var datePart))
                {
                    return new SqlFunctionExpression(
                        "DATEPART",
                        memberExpression.Type,
                        arguments: new[] { new SqlFragmentExpression(datePart), memberExpression.Expression });
                }

                switch (memberName)
                {
                    case nameof(LocalDateTime.Date):
                        return new SqlFunctionExpression(
                            "CONVERT",
                            memberExpression.Type,
                            new[] { new SqlFragmentExpression("date"), memberExpression.Expression });

                }
            }

            return null;
        }

    }

}
