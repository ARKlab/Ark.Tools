using Ark.Tools.Core;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Sql
{
    public interface ISqlContextAsync<Tag> : IContextAsync
    {
        DbConnection Connection { get; }
        DbTransaction? Transaction { get; }
        ValueTask RollbackAsync(CancellationToken ctk);
        //ValueTask Create(IsolationLevel isolationLevel, CancellationToken ctk);
        //ValueTask CommitAsync(CancellationToken ctk);
        //ValueTask CreateConnectionManager(string connectionString);
    }
}
