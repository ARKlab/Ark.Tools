// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Data.Common;
using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that filters out SQL dependency spans
/// targeting a specific database server and database (typically the NLog audit/log database).
/// As an <see cref="IArkSamplingFilter"/>, it also drops them before <see cref="ArkAdaptiveSampler"/> samples them.
/// </summary>
public sealed class ArkSqlDependencyFilterProcessor : BaseProcessor<Activity>, IArkSamplingFilter
{
    // Set by AddArkOpenTelemetry: a span filtered here, after sampling, gives its budget back.
    internal ArkAdaptiveSampler? _sampler;

    private readonly string? _configuredServer;
    private readonly string? _database;
    // SqlClient instrumentation reports db.namespace as "{instance}.{database}" for a named instance.
    private readonly string? _instanceDatabase;
    private readonly bool _enabled;

    /// <summary>
    /// Initializes a new instance of <see cref="ArkSqlDependencyFilterProcessor"/>.
    /// </summary>
    /// <param name="sqlConnectionString">
    /// The SQL connection string whose <c>Data Source</c> and <c>Initial Catalog</c>
    /// identify the database to filter. If <see langword="null"/> or empty, the processor is disabled.
    /// </param>
    public ArkSqlDependencyFilterProcessor(
        [Secret] string? sqlConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(sqlConnectionString))
        {
            try
            {
                var builder = new DbConnectionStringBuilder
                {
                    ConnectionString = sqlConnectionString,
                };
                var dataSource = _read(builder, "Data Source", "DataSource", "Server", "Address", "Addr", "Network Address");
                _database = _read(builder, "Initial Catalog", "InitialCatalog", "Database");
                // Match the instrumentation's server.address: no "tcp:"-style protocol prefix, port or instance name.
                var server = dataSource?.Split(',', 2)[0].Trim();
                var protocolEnd = server?.IndexOf(':', StringComparison.Ordinal) ?? -1;
                if (protocolEnd >= 0 && !server!.StartsWith('['))
                    server = server[(protocolEnd + 1)..].TrimStart('\\', '/').Trim();
                var instanceStart = server?.IndexOf('\\', StringComparison.Ordinal) ?? -1;
                if (instanceStart >= 0)
                {
                    _instanceDatabase = server![(instanceStart + 1)..] + "." + _database;
                    server = server[..instanceStart];
                }

                _configuredServer = server;
                _enabled = !string.IsNullOrWhiteSpace(_configuredServer) &&
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
        _filter(data);
    }

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        _filter(data);
    }

    /// <inheritdoc/>
    public bool ShouldDrop(in SamplingParameters samplingParameters)
    {
        // SqlClient instrumentation passes these tags when it creates the span.
        if (!_enabled || samplingParameters.Kind != ActivityKind.Client || samplingParameters.Tags is null)
            return false;

        string? dbSystem = null, peerName = null, dbName = null;
        foreach (var tag in samplingParameters.Tags)
        {
            switch (tag.Key)
            {
                case "db.system" or "db.system.name":
                    dbSystem ??= tag.Value as string;
                    break;
                case "server.address" or "net.peer.name" or "peer.service":
                    peerName ??= tag.Value as string;
                    break;
                case "db.name" or "db.namespace":
                    dbName ??= tag.Value as string;
                    break;
            }
        }

        return _matches(dbSystem, peerName, dbName);
    }

    private void _filter(Activity data)
    {
        if (data.IsAllDataRequested && _matches(
                data.GetTagItem("db.system") as string ?? data.GetTagItem("db.system.name") as string,
                data.GetTagItem("server.address") as string
                    ?? data.GetTagItem("net.peer.name") as string
                    ?? data.GetTagItem("peer.service") as string,
                data.GetTagItem("db.name") as string ?? data.GetTagItem("db.namespace") as string))
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            data.IsAllDataRequested = false;
            _sampler?._release(data);
        }
    }

    private bool _matches(string? dbSystem, string? peerName, string? dbName)
    {
        if (!_enabled)
            return false;

        if (!string.Equals(dbSystem, "mssql", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(dbSystem, "microsoft.sql", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(dbSystem, "microsoft.sql_server", StringComparison.OrdinalIgnoreCase))
            return false;

        // Match by server name.
        var configuredServer = _configuredServer!;
        return peerName != null &&
            (peerName.Equals(configuredServer, StringComparison.OrdinalIgnoreCase) ||
             peerName.Contains(configuredServer, StringComparison.OrdinalIgnoreCase)) &&
            dbName != null &&
            (string.Equals(dbName, _database, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(dbName, _instanceDatabase, StringComparison.OrdinalIgnoreCase));
    }

    private static string? _read(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value))
                return value as string;
        }

        return null;
    }
}
