using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Core
{
    public interface IDataContextAsync
    {
        Task<T> CreateAsync<T>(IsolationLevel isolationLevel, CancellationToken cancellationToken);
    }
}
