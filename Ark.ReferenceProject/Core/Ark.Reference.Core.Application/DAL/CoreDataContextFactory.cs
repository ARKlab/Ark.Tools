using Ark.Tools.Solid;
using Ark.Tools.Sql;

using Ark.Reference.Core.Application.Config;

using NodaTime;

using System.Data.Common;
using System.Security.Claims;

namespace Ark.Reference.Core.Application.DAL
{
    public sealed class CoreDataContextFactory
    {
        private readonly ICoreDataContextConfig _config;
        private readonly IDbConnectionManager _connectionManager;
        private readonly IClock _clock;

        private readonly IContextProvider<ClaimsPrincipal> _userContext;

        public CoreDataContextFactory(ICoreDataContextConfig config, IDbConnectionManager connectionManager, IClock clock, IContextProvider<ClaimsPrincipal> userContext)
        {
            _config = config;
            _connectionManager = connectionManager;
            _clock = clock;
            _userContext = userContext;
        }

        public ICoreDataContext Get(bool readOnly = false)
        {
            DbConnection conn = null;
            try
            {
                var cs = _config.SQLConnectionString;
                if (!cs.EndsWith(';')) cs += ";";
                if (readOnly) cs += "ApplicationIntent=ReadOnly;";

                conn = _connectionManager.Get(cs);
                return new CoreDataContext_Sql(conn, _config, _clock, _userContext,
                    // this is 'risk avoidance'.
                    // ReadCommitted, even when READ_COMMITTED_SNAPSHOT_ISOLATION is enabled, doesn't guarantee READ consistency between multiple statements
                    // Transaction may commit in the middle and new data is available.
                    // RCSI only avoid read locks if a record is UPDATED but not COMMITED returning the initial version.
                    // It's debetable we should use SNAPSHOT also for Write transactions ...
                    readOnly ? System.Data.IsolationLevel.Snapshot : System.Data.IsolationLevel.ReadCommitted);
            }
            catch
            {
                conn?.Dispose();
                throw;
            }
        }
    }

}
