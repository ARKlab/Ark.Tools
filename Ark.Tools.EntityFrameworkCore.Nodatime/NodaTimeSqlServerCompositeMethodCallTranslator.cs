// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators.Internal;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class NodaTimeSqlServerCompositeMethodCallTranslator : SqlServerCompositeMethodCallTranslator
    {
        private static readonly IMethodCallTranslator[] _methodCallTranslators = new IMethodCallTranslator[]
        {
            new SqlServerNodaTimeAddTranslator(), new SqlServerNodaTimeToSystemTypeTranslator()
        };

        public NodaTimeSqlServerCompositeMethodCallTranslator(RelationalCompositeMethodCallTranslatorDependencies dependencies)
            : base(dependencies)
        {
            AddTranslators(_methodCallTranslators);
        }
    }

}
