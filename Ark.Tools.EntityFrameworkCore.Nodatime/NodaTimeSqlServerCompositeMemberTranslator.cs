// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators.Internal;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class NodaTimeSqlServerCompositeMemberTranslator : SqlServerCompositeMemberTranslator
    {
        private static readonly IMemberTranslator[] _memberTranslators = new IMemberTranslator[] {
                  new SqlServerLocalDateMemberTranslator()
                , new SqlServerLocalDateTimeMemberTranslator()
                , new SqlServerInstantMemberTranslator()
                , new SqlServerOffsetDateTimeMemberTranslator()
            };

        public NodaTimeSqlServerCompositeMemberTranslator(RelationalCompositeMemberTranslatorDependencies dependencies)
            : base(dependencies)
        {
            AddTranslators(_memberTranslators);
        }
    }

}
