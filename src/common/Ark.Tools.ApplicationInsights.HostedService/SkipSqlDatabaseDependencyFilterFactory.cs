using Ark.Tools.AspNetCore.ApplicationInsights;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.ApplicationInsights.HostedService(net10.0)', Before:
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
=======
namespace Ark.Tools.ApplicationInsights.HostedService;

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
>>>>>>> After


namespace Ark.Tools.ApplicationInsights.HostedService;

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