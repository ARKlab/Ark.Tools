using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Solid;

using NLog;

using NodaTime;

using System.Data.Common;
using System.Security.Claims;

namespace Ark.Reference.Core.Application.DAL
{

    public sealed partial class CoreDataContext_Sql : AbstractSqlAsyncContextWithOutbox<CoreDataSql>, ICoreDataContext
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IClock _clock;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;
        //private readonly int _longRunningCommandTimeout = Convert.ToInt32(TimeSpan.FromMinutes(5).TotalSeconds);
        private readonly AuditContext<AuditKind> _auditContext;

        internal CoreDataContext_Sql(DbTransaction transaction,
                                    IOutboxContextSqlConfig config,
                                    IClock clock,
                                    IContextProvider<ClaimsPrincipal> _userContext)
            : base(transaction, config)
        {
            _clock = clock;
            this._userContext = _userContext;

            _auditContext = new AuditContext<AuditKind>(_logger, Connection, Transaction);
        }
    }
}