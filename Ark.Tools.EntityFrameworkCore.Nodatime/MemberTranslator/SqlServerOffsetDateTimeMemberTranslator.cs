// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using NodaTime;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class SqlServerOffsetDateTimeMemberTranslator : IMemberTranslator
    {
        private static readonly Dictionary<string, string> _datePartMapping
               = new Dictionary<string, string>
               {
                { nameof(OffsetDateTime.Year), "year" },
                { nameof(OffsetDateTime.Month), "month" },
                { nameof(OffsetDateTime.DayOfYear), "dayofyear" },
                { nameof(OffsetDateTime.Day), "day" },
                { nameof(OffsetDateTime.Hour), "hour" },
                { nameof(OffsetDateTime.Minute), "minute" },
                { nameof(OffsetDateTime.Second), "second" },
                { nameof(OffsetDateTime.Millisecond), "millisecond" },
                { nameof(OffsetDateTime.NanosecondOfSecond), "nanosecond" }
               };

        public Expression Translate(MemberExpression memberExpression)
        {
            var declaringType = memberExpression.Member.DeclaringType;
            if (declaringType == typeof(OffsetDateTime))
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
                    case nameof(OffsetDateTime.LocalDateTime):
                        return new SqlFunctionExpression(
                            "CONVERT",
                            memberExpression.Type,
                            new[] { new SqlFragmentExpression("datetime2"), memberExpression.Expression });

                    case nameof(OffsetDateTime.Date):
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
