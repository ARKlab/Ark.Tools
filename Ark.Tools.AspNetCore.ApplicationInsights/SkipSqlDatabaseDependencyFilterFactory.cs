// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Extensibility;

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
}
