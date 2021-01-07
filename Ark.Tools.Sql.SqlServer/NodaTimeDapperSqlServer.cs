using Ark.Tools.Nodatime.Dapper;

using Microsoft.Data.SqlClient;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Sql.SqlServer
{
    public static class NodatimeDapperSqlServer
    {
        public static void Setup()
        {
            NodaTimeDapper.Setup();
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
    }
}
