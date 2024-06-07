// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Dapper;

using Microsoft.Data.SqlClient;


namespace Ark.Tools.Sql.SqlServer
{
    public static class NodaTimeDapperSqlServer
    {
        static NodaTimeDapperSqlServer()
        {
            InstantHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is SqlParameter sql)
                {
                    sql.SqlDbType = System.Data.SqlDbType.DateTime2;
                }
            };
            LocalDateHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is SqlParameter sql)
                {
                    sql.SqlDbType = System.Data.SqlDbType.Date;
                }
            };
            LocalDateTimeHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is SqlParameter sql)
                {
                    sql.SqlDbType = System.Data.SqlDbType.DateTime2;
                }
            };
            OffsetDateTimeHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is SqlParameter sql)
                {
                    sql.SqlDbType = System.Data.SqlDbType.DateTimeOffset;
                }
            };
            LocalTimeHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is SqlParameter sql)
                {
                    sql.SqlDbType = System.Data.SqlDbType.Time;
                }
            };
        }

        public static void Setup()
        {
            NodaTimeDapper.Setup();
        }
    }
}
