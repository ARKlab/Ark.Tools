// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Data.SqlClient;

using System.Data.Common;
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
            }
            catch
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
                await conn.OpenAsync(ctk).ConfigureAwait(false);
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