// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using NodaTime.Text;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class OffsetDateTimeRelationalTypeMapping : RelationalTypeMapping
    {
        private const string OffsetDateTimeFormatConst = @"{0:yyyy-MM-dd HH\:mm\:ss.fffffffzzz}";

        /// <summary>
        ///     Initializes a new instance of the <see cref="DateTimeTypeMapping" /> class.
        /// </summary>
        /// <param name="storeType"> The name of the database type. </param>
        /// <param name="dbType"> The <see cref="DbType" /> to be used. </param>
        public OffsetDateTimeRelationalTypeMapping(
            string storeType,
            DbType? dbType = null)
                : base(storeType, typeof(OffsetDateTime), dbType)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="DateTimeTypeMapping" /> class.
        /// </summary>
        /// <param name="parameters"> Parameter object for <see cref="RelationalTypeMapping" />. </param>
        protected OffsetDateTimeRelationalTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        /// <summary>
        ///     Creates a copy of this mapping.
        /// </summary>
        /// <param name="parameters"> The parameters for this mapping. </param>
        /// <returns> The newly created mapping. </returns>
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new OffsetDateTimeRelationalTypeMapping(parameters);

        /// <summary>
        ///     Gets the string format to be used to generate SQL literals of this type.
        /// </summary>
        protected override string SqlLiteralFormatString => "TIMESTAMP '" + OffsetDateTimeFormatConst + "'";

        public override Expression GenerateCodeLiteral(object value)
        {
            if (value is OffsetDateTime i)
            {
                var s = OffsetDateTimePattern.ExtendedIso.Format(i);
                return Expression.Call(
                    typeof(OffsetDateTimeRelationalTypeMapping).GetMethod("FromIso"),
                    Expression.Constant(s, typeof(string))
                    );
            }

            return base.GenerateCodeLiteral(value);
        }

        public static OffsetDateTime FromIso(string s)
        {
            return OffsetDateTimePattern.ExtendedIso.Parse(s).GetValueOrThrow();
        }
    }

}
