using Ark.Tools.Core;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public interface IContextFactory<T>
    {
        ValueTask<T> CreateAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken);
    }
}
