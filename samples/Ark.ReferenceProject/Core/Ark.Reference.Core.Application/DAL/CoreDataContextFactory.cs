using Ark.Reference.Core.Application.Config;
using Ark.Tools.Core;
using Ark.Tools.Outbox;
using Ark.Tools.Solid;
using Ark.Tools.Sql;

using NodaTime;

using System.Data.Common;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

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
            return await CreateAsync(ctk).ConfigureAwait(false);
        }

        async Task<IOutboxAsyncContext> IOutboxAsyncContextFactory.CreateAsync(CancellationToken ctk)
        {
            return await CreateAsync(ctk).ConfigureAwait(false);
        }
    }

}
