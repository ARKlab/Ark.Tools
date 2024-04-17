using Ark.Tools.Core;
using Ark.Tools.Sql;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public class TestContextFactory<T> : IContextFactory2<T>//, IExternalContext
    {
        private ScenarioContext _currentContext;
        IExternalContext _externalContext;

        //private IExternalContext _inner => _currentContext.Get<IExternalContext>();

        public TestContextFactory(Func<ScenarioContext> scenarioContext)
        {
            _currentContext = scenarioContext.Invoke();
        }

        ValueTask<IExternalContext> IContextFactory2<T>.CreateAsync(ISQLConnectionString connectionStrin, IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            _externalContext = new ExternalContext();

            return ValueTask.FromResult(_externalContext);
            //return ValueTask.FromResult((AbstractSqlContextAsync<T>)_inner);
        }

        //void IExternalContext.ReadData()
        //{
        //    _inner.ReadData();
        //}

        public async ValueTask DisposeAsync()
        {
            await _externalContext.DisposeAsync();
        }
    }
}
