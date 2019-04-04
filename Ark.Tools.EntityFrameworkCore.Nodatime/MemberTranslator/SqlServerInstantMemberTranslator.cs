// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using NodaTime;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class SqlServerInstantMemberTranslator : IMemberTranslator
    {
        public Expression Translate(MemberExpression memberExpression)
        {
            var declaringType = memberExpression.Member.DeclaringType;
            if (declaringType == typeof(Instant))
            {
                var memberName = memberExpression.Member.Name;
            }

            return null;
        }
    }

}
