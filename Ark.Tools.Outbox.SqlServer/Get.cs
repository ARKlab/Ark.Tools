using EnsureThat;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    internal class Get_AnalyticGroupSearchQueryHandler
    {
        private readonly Func<IContextFactory<IRECDataContext>> _recDataContextFactory;

        public Get_AnalyticGroupSearchQueryHandler(Func<IContextFactory<IRECDataContext>> recDataContextFactory)
        {
            EnsureArg.IsNotNull(recDataContextFactory, nameof(recDataContextFactory));
            
            _recDataContextFactory = recDataContextFactory;
        }
        public int Execute(string query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<int> ExecuteAsync(string query, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(query, nameof(query));

            await using var ctx = await _recDataContextFactory().Create(IsolationLevel.ReadCommitted, ctk);

            var result = await ctx.;

            return new PagedResult<AnalyticGroupDto.V1.Output>()
            {
                Count = result.count,
                Data = result.data,
                IsCountPartial = false,
                Limit = query.Limit,
                Skip = query.Skip
            };
        }
    }
}
