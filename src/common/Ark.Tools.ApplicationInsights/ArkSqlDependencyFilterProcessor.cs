// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Data.SqlClient;

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that filters out SQL dependency spans
/// targeting a specific database server and database (typically the NLog audit/log database).
/// </summary>
public sealed class ArkSqlDependencyFilterProcessor : BaseProcessor<Activity>
{
    private readonly string? _dataSource;
    private readonly string? _database;
    private readonly bool _enabled;

    /// <summary>
    /// Initializes a new instance of <see cref="ArkSqlDependencyFilterProcessor"/>.
    /// </summary>
    /// <param name="sqlConnectionString">
    /// The SQL connection string whose <c>Data Source</c> and <c>Initial Catalog</c>
    /// identify the database to filter. If <see langword="null"/> or empty, the processor is disabled.
    /// </param>
    public ArkSqlDependencyFilterProcessor(string? sqlConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(sqlConnectionString))
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlConnectionString);
                _dataSource = builder.DataSource;
                _database = builder.InitialCatalog;
                _enabled = !string.IsNullOrWhiteSpace(_dataSource) &&
                           !string.IsNullOrWhiteSpace(_database);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _enabled = false;
            }
        }
    }

    /// <inheritdoc/>
    public override void OnStart(Activity data)
    {
        if (!_enabled)
            return;

        var dbSystem = data.GetTagItem("db.system") as string
                    ?? data.GetTagItem("db.system.name") as string;

        if (!string.Equals(dbSystem, "mssql", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(dbSystem, "microsoft.sql", StringComparison.OrdinalIgnoreCase))
            return;

        // Match by server name.
        var peerName = data.GetTagItem("server.address") as string
                    ?? data.GetTagItem("net.peer.name") as string
                    ?? data.GetTagItem("peer.service") as string;

        if (peerName != null &&
            peerName.Contains(_dataSource!, StringComparison.OrdinalIgnoreCase))
        {
            var dbName = data.GetTagItem("db.name") as string
                      ?? data.GetTagItem("db.namespace") as string;
            if (dbName != null &&
                string.Equals(dbName, _database, StringComparison.OrdinalIgnoreCase))
            {
                data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                data.IsAllDataRequested = false;
            }
        }
    }
}
