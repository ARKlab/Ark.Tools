// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies that <see cref="ArkSqlDependencyFilterProcessor"/> matches the tags the SqlClient instrumentation
/// reports for the NLog database.
/// </summary>
[TestClass]
public sealed class ArkSqlDependencyFilterProcessorTests
{
    private const string _logsDatabase = "Server=tcp:logs.example.com,1433;Database=Logs";

    [TestMethod]
    [DataRow("Server=logs.example.com;Database=Logs", "logs.example.com", "Logs", true)]
    [DataRow(_logsDatabase, "logs.example.com", "Logs", true)]
    [DataRow(@"Data Source=logs.example.com\SQLEXPRESS;Initial Catalog=Logs", "logs.example.com", "SQLEXPRESS.Logs", true)]
    [DataRow(_logsDatabase, "logs.example.com", "Orders", false)]
    [DataRow(_logsDatabase, "orders.example.com", "Logs", false)]
    public void OnEnd_DropsOnlySpansToTheConfiguredDatabase(
        string nlogDatabase,
        string host,
        string dbNamespace,
        bool expectFiltered)
    {
        using var processor = new ArkSqlDependencyFilterProcessor(nlogDatabase);
        using var activity = new Activity("sql");
        activity.Start();
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.SetTag("db.system.name", "microsoft.sql_server");
        activity.SetTag("server.address", host);
        activity.SetTag("db.namespace", dbNamespace);

        processor.OnEnd(activity);

        activity.Recorded.Should().Be(!expectFiltered);
    }

    [TestMethod]
    public void AddArkOpenTelemetry_DropsNLogDatabaseSpansFromConfiguration()
    {
        const string sourceName = "Ark.Tools.OTel.Tests.NLogDatabase";
        using var collector = new CollectingProcessor();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                // Azure App Service on Linux turns the '.' of a connection string name into '_'.
                ["ConnectionStrings:NLog_Database"] = _logsDatabase,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOpenTelemetry().AddArkOpenTelemetry(configuration: configuration);
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddSource(sourceName)
            .AddProcessor(collector));
        using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(sourceName);

        _startSqlSpan(source, "Logs")?.Stop();
        _startSqlSpan(source, "Orders")?.Stop();

        collector.Spans.Select(static span => span.GetTagItem("db.namespace")).Should().Equal("Orders");
    }

    private static Activity? _startSqlSpan(ActivitySource source, string database)
    {
        return source.StartActivity(ActivityKind.Client, tags:
        [
            new("db.system.name", "microsoft.sql_server"),
            new("server.address", "logs.example.com"),
            new("db.namespace", database),
        ]);
    }
}
