using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public interface IServiceFactory<in TKey>
    {
        object Create(TKey key, IsolationLevel isolation, CancellationToken ctk);
    }
}
