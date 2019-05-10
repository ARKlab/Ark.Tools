// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
using System.Data.SqlClient;

namespace Ark.Tools.Sql.SqlServer
{
    public class ReliableSqlConnectionManager : SqlConnectionManager
    {
        protected override SqlConnection Build(string connectionString)
        {
            var opt = new SqlConnectionStringBuilder(connectionString);
            opt.ConnectRetryCount = 3;
            opt.ConnectRetryInterval = 10;
            opt.ConnectTimeout = 30;
            opt.MultipleActiveResultSets = true;

            var conn = new SqlConnection(opt.ConnectionString);
            conn.InfoMessage += new SqlInfoMessageEventHandler(OnInfoMessage);
            conn.FireInfoMessageEventOnUserErrors = false;

            return conn;
        }
    }
}
