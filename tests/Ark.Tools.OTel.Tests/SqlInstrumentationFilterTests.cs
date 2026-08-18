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
public sealed class SqlInstrumentationFilterTests
{
    private const string ConnectionStringEnvironmentVariable = "ARK_SQL_CONNECTION_STRING";

    /// <summary>
    /// The configured SQL database filter suppresses a dependency span for the target database
    /// while retaining a dependency span for another database on the same server.
    /// </summary>
    [TestMethod]
    [TestCategory("integration")]
    public void SqlDependencyFilter_WithSqlClientInstrumentation_FiltersTargetDatabaseOnly()
    {
        var connectionString = _getConnectionString();
        if (connectionString is null)
            Assert.Inconclusive($"Set {ConnectionStringEnvironmentVariable} to run SQL integration tests.");

        var target = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
        };
        var targetDatabase = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        using var pipeline = _createPipeline(new ArkSqlDependencyFilterProcessor(connectionString));
        _executeScalar(connectionString);
        _executeScalar(target.ConnectionString);

        pipeline.Exported.Should().ContainSingle(span =>
            string.Equals(span.GetTagItem("db.name") as string, targetDatabase, StringComparison.OrdinalIgnoreCase));
        pipeline.Exported.Should().NotContain(span =>
            string.Equals(span.GetTagItem("db.name") as string, "master", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The pre-filter suppresses a SQL transaction commit span emitted by SQL client instrumentation.
    /// </summary>
    [TestMethod]
    [TestCategory("integration")]
    public void PreFilter_WithSqlClientInstrumentation_FiltersCommit()
    {
        var connectionString = _getConnectionString();
        if (connectionString is null)
            Assert.Inconclusive($"Set {ConnectionStringEnvironmentVariable} to run SQL integration tests.");

        using var pipeline = _createPipeline(new ArkPreFilterProcessor());
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1";
        command.ExecuteScalar();
        transaction.Commit();

        pipeline.Exported.Should().NotContain(span =>
            string.Equals(span.GetTagItem("db.operation.name") as string, "Commit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(span.GetTagItem("db.operation") as string, "Commit", StringComparison.OrdinalIgnoreCase));
    }

    private static TestPipeline _createPipeline(BaseProcessor<Activity> processor)
    {
        return new TestPipeline(
            "Microsoft.Data.SqlClient",
            new AlwaysOnSampler(),
            processor);
    }

    private static void _executeScalar(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.ExecuteScalar();
    }

    private static string? _getConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            using var connection = new SqlConnection(value);
            connection.Open();
            return value;
        }
        catch (SqlException)
        {
            return null;
        }
    }
}
