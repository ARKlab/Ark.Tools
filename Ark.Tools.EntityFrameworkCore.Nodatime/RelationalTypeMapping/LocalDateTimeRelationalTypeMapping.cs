// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using NodaTime.Text;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class LocalDateTimeRelationalTypeMapping : RelationalTypeMapping
    {
        private const string LocalDateTimeFormatConst = @"{0:yyyy-MM-dd HH\:mm\:ss.fffffff}";

        /// <summary>
        ///     Initializes a new instance of the <see cref="DateTimeTypeMapping" /> class.
        /// </summary>
        /// <param name="storeType"> The name of the database type. </param>
        /// <param name="dbType"> The <see cref="DbType" /> to be used. </param>
        public LocalDateTimeRelationalTypeMapping(
            string storeType,
            DbType? dbType = null)
                : base(storeType, typeof(LocalDateTime), dbType)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="DateTimeTypeMapping" /> class.
        /// </summary>
        /// <param name="parameters"> Parameter object for <see cref="RelationalTypeMapping" />. </param>
        protected LocalDateTimeRelationalTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        /// <summary>
        ///     Creates a copy of this mapping.
        /// </summary>
        /// <param name="parameters"> The parameters for this mapping. </param>
        /// <returns> The newly created mapping. </returns>
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new LocalDateTimeRelationalTypeMapping(parameters);

        /// <summary>
        ///     Gets the string format to be used to generate SQL literals of this type.
        /// </summary>
        protected override string SqlLiteralFormatString => "TIMESTAMP '" + LocalDateTimeFormatConst + "'";


        private static readonly MethodInfo _dbDataReaderMethod = typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) });
        public override MethodInfo GetDataReaderMethod()
            => _dbDataReaderMethod;

        public override Expression CustomizeDataReaderExpression(Expression expression)
        {
            return Expression.Call(null, typeof(LocalDateTime).GetRuntimeMethod(nameof(LocalDateTime.FromDateTime), new[] { typeof(DateTime) })
                , expression
                );
        }

        public override Expression GenerateCodeLiteral(object value)
        {
            if (value is LocalDateTime ldt)
            {
                var s = LocalDateTimePattern.ExtendedIso.Format(ldt);
                return Expression.Call(
                    typeof(LocalDateTimeRelationalTypeMapping).GetMethod("FromIso"),
                    Expression.Constant(s, typeof(string))
                    );
            }

            return base.GenerateCodeLiteral(value);
        }

        public static LocalDateTime FromIso(string s)
        {
            return LocalDateTimePattern.ExtendedIso.Parse(s).GetValueOrThrow();
        }
    }

}
