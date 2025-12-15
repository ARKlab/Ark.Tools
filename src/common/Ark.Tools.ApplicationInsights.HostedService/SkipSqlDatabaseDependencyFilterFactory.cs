using Ark.Tools.AspNetCore.ApplicationInsights;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;

namespace Ark.Tools.ApplicationInsights.HostedService
{
    public class SkipSqlDatabaseDependencyFilterFactory : ITelemetryProcessorFactory
    {
        private readonly string _sqlConnection;

        public SkipSqlDatabaseDependencyFilterFactory(string sqlConnection)
        {
            this._sqlConnection = sqlConnection;
        }

        public ITelemetryProcessor Create(ITelemetryProcessor nextProcessor)
        {
            return new SkipSqlDatabaseDependencyFilter(nextProcessor, _sqlConnection);
        }
    }
}
