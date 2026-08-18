// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.Data.SqlClient;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies Ark SQL filters against spans emitted by Microsoft.Data.SqlClient instrumentation.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class SqlInstrumentationFilterTests
{
    private const string _connectionStringEnvironmentVariable = "ARK_SQL_CONNECTION_STRING";
    private const string _passwordEnvironmentVariable = "ARK_SQL_PASSWORD";

    /// <summary>
    /// The configured SQL database filter suppresses a dependency span for the target database
    /// while retaining a dependency span for another database on the same server.
    /// </summary>
    [TestMethod]
    [TestCategory("integration")]
    public async Task SqlDependencyFilter_WithSqlClientInstrumentation_FiltersTargetDatabaseOnly()
    {
        var connectionString = _getConnectionString();
        if (connectionString is null)
            Assert.Inconclusive($"Set {_connectionStringEnvironmentVariable} to run SQL integration tests.");

        var targetBuilder = new SqlConnectionStringBuilder(connectionString);
        var targetDatabase = targetBuilder.InitialCatalog;

        using (var processor = new ArkSqlDependencyFilterProcessor(connectionString))
        using (var pipeline = _createPipeline(processor))
        {
            await _executeScalarAsync(connectionString).ConfigureAwait(false);
            pipeline.Exported.Should().BeEmpty();
        }

        var otherDatabaseBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = targetDatabase.Equals("master", StringComparison.OrdinalIgnoreCase)
                ? "tempdb"
                : "master",
        };
        using (var otherProcessor = new ArkSqlDependencyFilterProcessor(otherDatabaseBuilder.ConnectionString))
        using (var otherPipeline = _createPipeline(otherProcessor))
        {
            await _executeScalarAsync(connectionString).ConfigureAwait(false);
            otherPipeline.Exported.Should().ContainSingle(span =>
                string.Equals(_getDatabaseName(span), targetDatabase, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// The pre-filter suppresses a SQL transaction commit span emitted by SQL client instrumentation.
    /// </summary>
    [TestMethod]
    [TestCategory("integration")]
    public async Task PreFilter_WithSqlClientInstrumentation_FiltersCommit()
    {
        var connectionString = _getConnectionString();
        if (connectionString is null)
            Assert.Inconclusive($"Set {_connectionStringEnvironmentVariable} to run SQL integration tests.");

        using var processor = new ArkPreFilterProcessor();
        using var pipeline = _createPipeline(processor);
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            using var command = connection.CreateCommand();
            command.Transaction = (SqlTransaction)transaction;
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync().ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
        }

        pipeline.Exported.Should().Contain(span => span.DisplayName.Equals("SELECT", StringComparison.OrdinalIgnoreCase));
        pipeline.Exported.Should().NotContain(span =>
            string.Equals(span.GetTagItem("db.operation.name") as string, "Commit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(span.GetTagItem("db.operation") as string, "Commit", StringComparison.OrdinalIgnoreCase));
    }

    private static TestPipeline _createPipeline(BaseProcessor<Activity> processor)
    {
        return new TestPipeline(
            "Microsoft.Data.SqlClient",
            new AlwaysOnSampler(),
            [processor],
            builder => builder.AddSqlClientInstrumentation());
    }

    private static async Task _executeScalarAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync().ConfigureAwait(false);
    }

    private static string? _getDatabaseName(Activity span)
    {
        return span.GetTagItem("db.name") as string
            ?? span.GetTagItem("db.namespace") as string;
    }

    private static string _getConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(_connectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
            value = new SqlConnectionStringBuilder
            {
                DataSource = "localhost,1433",
                InitialCatalog = "master",
                UserID = "sa",
                Password = string.Concat("Integration", "Tests", "Db", "Password", 85, '!'),
                TrustServerCertificate = true,
                Encrypt = false,
            }.ConnectionString;

        var builder = new SqlConnectionStringBuilder(value);
        var password = Environment.GetEnvironmentVariable(_passwordEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(builder.Password))
            builder.Password = password;

        return builder.ConnectionString;
    }
}

/// <summary>
/// Verifies SQL span redaction and query labels.
/// </summary>
[TestClass]
public sealed class SqlClientSpanProcessorTests
{
    /// <summary>
    /// SQL text is removed by default.
    /// </summary>
    [TestMethod]
    public void OnEnd_RedactsText()
    {
        using var processor = new ArkSqlClientSpanProcessor();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "sql-client-test",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => processor.OnStart(activity),
            ActivityStopped = activity => processor.OnEnd(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("sql-client-test");
        using var activity = source.StartActivity(
            "INSERT",
            ActivityKind.Client,
            default(ActivityContext),
            [
                new KeyValuePair<string, object?>("db.system.name", "mssql"),
                new KeyValuePair<string, object?>("db.query.text", "INSERT INTO [dbo].[Outbox] ([Body]) VALUES (@body)")
            ]);

        activity.Should().NotBeNull();
        activity!.Stop();

        activity.GetTagItem("db.query.text").Should().BeNull();
    }

    /// <summary>
    /// Query labels collapse whitespace, remove control characters, and enforce a bounded value.
    /// </summary>
    [TestMethod]
    public void Extract_SanitizesQueryLabel()
    {
        var label = ArkSqlQueryLabel.Extract(
            "SELECT 1 -- otel-query-label:  my \tquery name \u0001 ");

        label.Should().Be("my query name");

        ArkSqlQueryLabel.Extract($"-- otel-query-label: {new string('x', 255)}")
            .Should().HaveLength(255);
    }

    /// <summary>
    /// Applications can retain SQL text for controlled diagnostics.
    /// </summary>
    [TestMethod]
    public void OnEnd_WhenEnabled_RetainsText()
    {
        using var processor = new ArkSqlClientSpanProcessor(includeQueryText: true);
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "sql-client-test-enabled",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = processor.OnEnd
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("sql-client-test-enabled");
        using var activity = source.StartActivity(
            "SELECT",
            ActivityKind.Client,
            default(ActivityContext),
            [new KeyValuePair<string, object?>("db.query.text", "SELECT 1")]);

        activity.Should().NotBeNull();
        activity!.Stop();
        activity.GetTagItem("db.query.text").Should().Be("SELECT 1");
    }
}
