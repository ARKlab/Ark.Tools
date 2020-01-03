// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Data.SqlClient;

namespace Ark.Tools.AspNetCore.ApplicationInsights
{
    public class SkipSqlDatabaseDependencyFilter : ITelemetryProcessor
    {
        private ITelemetryProcessor _next;
        private SqlConnectionStringBuilder _sqlConnection;

        // Link processors to each other in a chain.
        public SkipSqlDatabaseDependencyFilter(ITelemetryProcessor next, string sqlConnection)
        {
            this._next = next;
            this._sqlConnection = new SqlConnectionStringBuilder(sqlConnection);
        }

        public void Process(ITelemetry item)
        {
            // To filter out an item, just return 
            if (!_oktoSend(item)) { return; }

            this._next.Process(item);
        }

        // Example: replace with your own criteria.
        private bool _oktoSend(ITelemetry item)
        {
            if (item is DependencyTelemetry d && d.Name.Contains(_sqlConnection.DataSource) && d.Name.Contains(_sqlConnection.InitialCatalog))
                return false;

            return true;
        }
    }
}
