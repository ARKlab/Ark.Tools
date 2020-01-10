using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;
using Ark.Tools.AspNetCore.ApplicationInsights;

namespace Ark.Tools.ApplicationInsights.HostedService
{
    public class SkipSqlDatabaseDependencyFilterFactory : ITelemetryProcessorFactory
    {
        private readonly string _sqlConnection;

        public SkipSqlDatabaseDependencyFilterFactory(string sqlConnection)
        {
            this._sqlConnection = sqlConnection;
        }

        public ITelemetryProcessor Create(ITelemetryProcessor next)
        {
            return new SkipSqlDatabaseDependencyFilter(next, _sqlConnection);
        }
    }
}
