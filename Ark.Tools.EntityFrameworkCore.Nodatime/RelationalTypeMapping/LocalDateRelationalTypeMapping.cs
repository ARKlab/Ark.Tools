// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using NodaTime.Text;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class LocalDateRelationalTypeMapping : RelationalTypeMapping
    {
        private const string LocalDateFormatConst = @"{0:yyyy-MM-dd}";

        /// <summary>
        ///     Initializes a new instance of the <see cref="DateTimeTypeMapping" /> class.
        /// </summary>
        /// <param name="storeType"> The name of the database type. </param>
        /// <param name="dbType"> The <see cref="DbType" /> to be used. </param>
        public LocalDateRelationalTypeMapping(
            string storeType,
            DbType? dbType = null)
                : base(storeType, typeof(LocalDate), dbType)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="DateTimeTypeMapping" /> class.
        /// </summary>
        /// <param name="parameters"> Parameter object for <see cref="RelationalTypeMapping" />. </param>
        protected LocalDateRelationalTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        /// <summary>
        ///     Creates a copy of this mapping.
        /// </summary>
        /// <param name="parameters"> The parameters for this mapping. </param>
        /// <returns> The newly created mapping. </returns>
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new LocalDateRelationalTypeMapping(parameters);

        /// <summary>
        ///     Gets the string format to be used to generate SQL literals of this type.
        /// </summary>
        protected override string SqlLiteralFormatString => "TIMESTAMP '" + LocalDateFormatConst + "'";

        public override Expression GenerateCodeLiteral(object value)
        {
            if (value is LocalDate ld)
            {
                var s = LocalDatePattern.Iso.Format(ld);
                return Expression.Call(
                    typeof(LocalDateRelationalTypeMapping).GetMethod("FromIso"),
                    Expression.Constant(s, typeof(string))
                    );
            }

            return base.GenerateCodeLiteral(value);
        }

        public static LocalDate FromIso(string s)
        {
            return LocalDatePattern.Iso.Parse(s).GetValueOrThrow();
        }
    }

}
