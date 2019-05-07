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
    public class InstantRelationalTypeMapping : RelationalTypeMapping
    {
        private const string InstantFormatConst = @"{0:yyyy-MM-dd HH\:mm\:ss.fffffff}";

        /// <summary>
        ///     Initializes a new instance of the <see cref="DateTimeTypeMapping" /> class.
        /// </summary>
        /// <param name="storeType"> The name of the database type. </param>
        /// <param name="dbType"> The <see cref="DbType" /> to be used. </param>
        public InstantRelationalTypeMapping(
            string storeType,
            DbType? dbType = null)
                : base(storeType, typeof(Instant), dbType)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="DateTimeTypeMapping" /> class.
        /// </summary>
        /// <param name="parameters"> Parameter object for <see cref="RelationalTypeMapping" />. </param>
        protected InstantRelationalTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        /// <summary>
        ///     Creates a copy of this mapping.
        /// </summary>
        /// <param name="parameters"> The parameters for this mapping. </param>
        /// <returns> The newly created mapping. </returns>
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new InstantRelationalTypeMapping(parameters);

        /// <summary>
        ///     Gets the string format to be used to generate SQL literals of this type.
        /// </summary>
        protected override string SqlLiteralFormatString => "TIMESTAMP '" + InstantFormatConst + "'";


        public override Expression GenerateCodeLiteral(object value)
        {
            if (value is Instant i)
            {
                var s = InstantPattern.ExtendedIso.Format(i);
                return Expression.Call(
                    typeof(InstantRelationalTypeMapping).GetMethod("FromIso"),
                    Expression.Constant(s, typeof(string))
                    );
            }

            return base.GenerateCodeLiteral(value);
        }

        private static readonly MethodInfo _dbDataReaderMethod = typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) });
        public override MethodInfo GetDataReaderMethod()
            => _dbDataReaderMethod;

        public override Expression CustomizeDataReaderExpression(Expression expression)
        {
            var specifyKind = Expression.Call(null, 
                typeof(DateTime).GetRuntimeMethod(nameof(DateTime.SpecifyKind), new[] { typeof(DateTime), typeof(DateTimeKind) }),
                expression, 
                Expression.Constant(DateTimeKind.Utc, typeof(DateTimeKind))
                );

            return Expression.Call(null, typeof(Instant).GetRuntimeMethod(nameof(Instant.FromDateTimeUtc), new[] { typeof(DateTime) })
                , specifyKind
                );
        }

        public static Instant FromIso(string s)
        {
            return InstantPattern.ExtendedIso.Parse(s).GetValueOrThrow();
        }
    }

}
