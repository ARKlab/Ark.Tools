using System.Data;
using System.Data.SqlClient;

namespace Ark.Tools.Sql.SqlServer
{
    public class ReliableSqlConnectionManager : IDbConnectionManager
    {
        private static void OnInfoMessage(object sender, SqlInfoMessageEventArgs ev)
        {
            SqlExceptionHandler.LogSqlInfoMessage(ev);
        }

        public IDbConnection Get(string connectionString)
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
