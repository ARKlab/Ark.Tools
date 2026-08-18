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
                InitialCatalog = "Ark.MediatorFramework.Sample",
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
