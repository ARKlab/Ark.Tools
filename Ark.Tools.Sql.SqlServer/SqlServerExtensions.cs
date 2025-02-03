// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Dapper;

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Ark.Tools.Sql.SqlServer
{
    public static class SqlServerExtensions
    {

        public static string AsSqlServerPagedQuery(this string query, string[] sortFields)
        {
            return $@"
                {query}

                ORDER BY {String.Join(", ", sortFields)}
                OFFSET @Skip ROWS FETCH NEXT @Limit ROWS ONLY

                SELECT COUNT(*) FROM({query}) a";
        }

        [Obsolete("Use AsSqlServerPagedQuery()", true)]
        public static string ConvertToPaged(this string query, string[] sortFields)
        {
            return $@"
                {query}

                ORDER BY {String.Join(", ", sortFields)}
                OFFSET @Skip ROWS FETCH NEXT @Limit ROWS ONLY

                SELECT COUNT(*) FROM({query}) a";
        }


        public static async Task<(IEnumerable<TReturn> data, int count)> ReadPagedAsync<TReturn>(this IDbConnection connection, CommandDefinition cmd)
        {
            var r = await connection.QueryMultipleAsync(cmd).ConfigureAwait(false);
            await using var _ = r.ConfigureAwait(false);

            var retVal = await r.ReadAsync<TReturn>().ConfigureAwait(false);
            var count = await r.ReadFirstAsync<int>().ConfigureAwait(false);

            return (retVal, count);
        }
    }
}