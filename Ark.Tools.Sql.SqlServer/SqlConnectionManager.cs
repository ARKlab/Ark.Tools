// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Sql.SqlServer
{
    public class SqlConnectionManager : IDbConnectionManager
    {
        protected static void OnInfoMessage(object sender, SqlInfoMessageEventArgs ev)
        {
            SqlExceptionHandler.LogSqlInfoMessage(ev);
        }

        public DbConnection Get(string connectionString)
        {
            var conn = Build(connectionString);
            try
            {
                conn.Open();
                return conn;
            } catch
            {
                conn?.Dispose();
                throw;
            }
        }

        public async Task<DbConnection> GetAsync(string connectionString, CancellationToken ctk = default)
        {
            var conn = Build(connectionString);
            try
            {
                await conn.OpenAsync(ctk);
                return conn;
            }
            catch
            {
                conn?.Dispose();
                throw;
            }
        }

        protected virtual SqlConnection Build(string connectionString)
        {
            var conn = new SqlConnection(connectionString);
            conn.InfoMessage += new SqlInfoMessageEventHandler(OnInfoMessage);
            conn.FireInfoMessageEventOnUserErrors = false;
            return conn;
        }
    }
}
