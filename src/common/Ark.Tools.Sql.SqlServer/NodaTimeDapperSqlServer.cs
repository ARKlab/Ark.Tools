// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Dapper;

using Microsoft.Data.SqlClient;


namespace Ark.Tools.Sql.SqlServer;

public static class NodaTimeDapperSqlServer
{
    static NodaTimeDapperSqlServer()
    {
        InstantHandler.Instance.OnSetValue += static (s, p) =>
        {
            if (p is SqlParameter sql)
            {
                sql.SqlDbType = System.Data.SqlDbType.DateTime2;
            }
        };
        LocalDateHandler.Instance.OnSetValue += static (s, p) =>
        {
            if (p is SqlParameter sql)
            {
                sql.SqlDbType = System.Data.SqlDbType.Date;
            }
        };
        LocalDateTimeHandler.Instance.OnSetValue += static (s, p) =>
        {
            if (p is SqlParameter sql)
            {
                sql.SqlDbType = System.Data.SqlDbType.DateTime2;
            }
        };
        OffsetDateTimeHandler.Instance.OnSetValue += static (s, p) =>
        {
            if (p is SqlParameter sql)
            {
                sql.SqlDbType = System.Data.SqlDbType.DateTimeOffset;
            }
        };
        LocalTimeHandler.Instance.OnSetValue += static (s, p) =>
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

    /// <summary>
    /// Sets the SQL Server parameter types (<c>datetime2</c>, <c>date</c>, <c>time</c>, <c>datetimeoffset</c>) on the shared
    /// <c>Ark.Tools.Nodatime.Dapper</c> handler instances, without registering them with Dapper's <c>SqlMapper</c>.
    /// </summary>
    /// <remarks>
    /// For Native AOT apps whose Dapper.AOT handlers reuse those instances: <see cref="Setup"/> registers the handlers
    /// with <c>SqlMapper.AddTypeHandler</c>, which is not Native AOT compatible.
    /// </remarks>
    public static void ConfigureParameterTypes()
    {
        // The static constructor does the work; calling a member is what runs it.
    }
}