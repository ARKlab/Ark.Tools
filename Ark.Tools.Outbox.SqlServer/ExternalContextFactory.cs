using Ark.Tools.Sql;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    internal class ExternalContextFactory<T> : IContextFactory2<T>, IAsyncDisposable
    {
        public ValueTask<AbstractSqlContextAsync<T>> CreateAsync(ISQLConnectionString connectionStrin, IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}
