using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Data.SqlClient;

namespace Ark.Tools.AspNetCore.ApplicationInsights
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

    public class SkipSqlDatabaseDependencyFilter : ITelemetryProcessor
    {
        private ITelemetryProcessor _next { get; }
        public SqlConnectionStringBuilder _sqlConnection { get; }

        // Link processors to each other in a chain.
        public SkipSqlDatabaseDependencyFilter(ITelemetryProcessor next, string sqlConnection)
        {
            this._next = next;
            this._sqlConnection = new SqlConnectionStringBuilder(sqlConnection);
        }

        public void Process(ITelemetry item)
        {
            // To filter out an item, just return 
            if (!OKtoSend(item)) { return; }

            this._next.Process(item);
        }

        // Example: replace with your own criteria.
        private bool OKtoSend(ITelemetry item)
        {
            if (item is DependencyTelemetry d && d.Name.Contains(_sqlConnection.DataSource) && d.Name.Contains(_sqlConnection.InitialCatalog))
                return false;

            return true;
        }
    }
}
