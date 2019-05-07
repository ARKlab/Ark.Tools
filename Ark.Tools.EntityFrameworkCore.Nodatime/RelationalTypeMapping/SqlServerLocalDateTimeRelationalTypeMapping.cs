// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class SqlServerLocalDateTimeRelationalTypeMapping : LocalDateTimeRelationalTypeMapping
    {
        private const string LocalDateTimeFormatConst = "{0:yyyy-MM-ddTHH:mm:ss.fffffffff}";

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public SqlServerLocalDateTimeRelationalTypeMapping(
            string storeType,
            DbType? dbType = System.Data.DbType.DateTime2)
            : base(storeType, dbType)
        {
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected SqlServerLocalDateTimeRelationalTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        /// <summary>
        ///     Creates a copy of this mapping.
        /// </summary>
        /// <param name="parameters"> The parameters for this mapping. </param>
        /// <returns> The newly created mapping. </returns>
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new SqlServerLocalDateTimeRelationalTypeMapping(parameters);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override string SqlLiteralFormatString => "'" + LocalDateTimeFormatConst + "'";

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override void ConfigureParameter(DbParameter parameter)
        {
            base.ConfigureParameter(parameter);

            if (parameter.Value is LocalDateTime ldt)
                parameter.Value = ldt.ToDateTimeUnspecified();

            if (Size.HasValue
                && Size.Value != -1)
            {
                parameter.Size = Size.Value;
            }
        }
    }

}
