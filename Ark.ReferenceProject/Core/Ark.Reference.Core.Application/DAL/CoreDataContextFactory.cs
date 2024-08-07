using Ark.Tools.Solid;
using Ark.Tools.Sql;

using Ark.Reference.Core.Application.Config;

using NodaTime;

using System.Data.Common;
using System.Security.Claims;
using Ark.Tools.Core;
using System.Threading.Tasks;
using System.Threading;
using Ark.Tools.Outbox;

namespace Ark.Reference.Core.Application.DAL
{
    public interface ICoreDataContextFactory : IAsyncContextFactory<ICoreDataContext> { }
    public sealed class CoreDataContextFactory : AbstractSqlAsyncContextFactory<CoreDataContext_Sql, CoreDataSql>, ICoreDataContextFactory, IOutboxAsyncContextFactory
    {
        private readonly ICoreDataContextConfig _config;
        private readonly IClock _clock;

        private readonly IContextProvider<ClaimsPrincipal> _userContext;

        public CoreDataContextFactory(ICoreDataContextConfig config, IDbConnectionManager connectionManager, IClock clock, IContextProvider<ClaimsPrincipal> userContext)
            : base(connectionManager, config)
        {
            _config = config;
            _clock = clock;
            _userContext = userContext;
        }

        protected override CoreDataContext_Sql CreateContext(DbTransaction transaction)
        {
            return new CoreDataContext_Sql(transaction, _config, _clock, _userContext);
        }

        async Task<ICoreDataContext> IAsyncContextFactory<ICoreDataContext>.CreateAsync(CancellationToken ctk)
        {
            return await base.CreateAsync(ctk);
        }

        async Task<IOutboxAsyncContext> IOutboxAsyncContextFactory.CreateAsync(CancellationToken ctk)
        {
            return await base.CreateAsync(ctk);
        }
    }

}
