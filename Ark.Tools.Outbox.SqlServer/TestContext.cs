using Ark.Tools.Sql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public class TestContext :AbstractSqlContextAsync<DataSql>,  IExternalContext, IAsyncDisposable
    {
        private ScenarioContext _currentContext;

        private IExternalContext _inner => _currentContext.Get<IExternalContext>();

        public TestContext(Func<ScenarioContext> scenarioContext)
        {
            _currentContext = scenarioContext.Invoke();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public int ReadData()
        {
            return _inner.ReadData();
        }
    }
}
