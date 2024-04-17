using Ark.Tools.Core;
using Ark.Tools.Sql;

using SimpleInjector;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public class ServiceFactory1<TKey>
    {
        //private IsolationLevel _isolationLevel;
        //private CancellationToken _cancellationToken;
        //private IContext _context;

        public ServiceFactory1(/*IsolationLevel isolationLevel, IContext context, CancellationToken cancellationToken*/) 
        {
            //_isolationLevel = isolationLevel;
            //_cancellationToken = cancellationToken;
            //_context = context;
        }

        public async Task<ISqlContextAsync<TKey>> Create(TKey key, IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ctk = default)
        {
            if (key != null)
            {
                ISqlContextAsync<TKey>? context = Activator.CreateInstance(key.GetType()) as ISqlContextAsync<TKey>;

                if (context != null)
                {
                    await context.Create(isolation, ctk);

                    return context;
                }
            }

            throw new ArgumentException("Not Exist");
        }
    }
}
