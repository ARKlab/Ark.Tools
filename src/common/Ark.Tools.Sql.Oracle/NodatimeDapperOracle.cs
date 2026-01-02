// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Dapper;

using Oracle.ManagedDataAccess.Client;

namespace Ark.Tools.Sql.Oracle
{
    public static class NodatimeDapperOracle
    {
        static NodatimeDapperOracle()
        {
            InstantHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is OracleParameter orc)
                {
                    orc.Precision = 9;
                    orc.OracleDbType = OracleDbType.TimeStamp;
                }
            };
            LocalDateHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is OracleParameter orc)
                {
                    orc.OracleDbType = OracleDbType.Date;
                }
            };
            LocalDateTimeHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is OracleParameter orc)
                {
                    orc.Precision = 9;
                    orc.OracleDbType = OracleDbType.TimeStamp;
                }
            };
            OffsetDateTimeHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is OracleParameter orc)
                {
                    orc.Precision = 9;
                    orc.OracleDbType = OracleDbType.TimeStampTZ;
                }
            };
            LocalTimeHandler.Instance.OnSetValue += (s, p) =>
            {
                if (p is OracleParameter orc)
                {
                    orc.Precision = 9;
                    orc.OracleDbType = OracleDbType.TimeStamp;
                }
            };
        }

        public static void Setup()
        {
            NodaTimeDapper.Setup();
        }
    }
}