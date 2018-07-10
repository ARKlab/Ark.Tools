// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
using System.Data.SqlClient;

namespace Ark.Tools.Sql.SqlServer
{
    public class SqlConnectionManager : IDbConnectionManager
    {
        private static void OnInfoMessage(object sender, SqlInfoMessageEventArgs ev)
        {
            SqlExceptionHandler.LogSqlInfoMessage(ev);
        }

        public IDbConnection Get(string connectionString)
        {
            var conn = new SqlConnection(connectionString);
            conn.InfoMessage += new SqlInfoMessageEventHandler(OnInfoMessage);
            conn.FireInfoMessageEventOnUserErrors = false;
            return conn;
        }
    }
}
