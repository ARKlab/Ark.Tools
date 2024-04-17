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
    public interface IContextFactory1<T>
    {        
        ValueTask<T> CreateAsync(ISQLConnectionString connectionStrin, IsolationLevel isolationLevel, CancellationToken cancellationToken);
    }

    public interface IContextFactory2<T>
    {        
        ValueTask<AbstractSqlContextAsync<T>> CreateAsync(ISQLConnectionString connectionStrin, IsolationLevel isolationLevel, CancellationToken cancellationToken);
    }
}
