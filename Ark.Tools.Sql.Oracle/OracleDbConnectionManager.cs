// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Ark.Tools.Sql.Oracle
{
    public class OracleDbConnectionManager : IDbConnectionManager
    {
        private static void OnInfoMessage(object sender, OracleInfoMessageEventArgs ev)
        {
            OracleExceptionHandler.LogSqlInfoMessage(ev);
        }

        public IDbConnection Get(string connectionString)
        {
            var conn = new OracleConnection(connectionString);
            
            conn.InfoMessage += new OracleInfoMessageEventHandler(OnInfoMessage);

            return conn;
        }
    }
}
