// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Oracle.ManagedDataAccess.Client;

using System.Reflection;

[assembly: DoNotParallelize]

namespace Ark.Tools.Sql.Oracle.Tests;

/// <summary>
/// Tests for OracleDbConnectionManager to verify CommandTimeout behavior.
/// </summary>
[TestClass]
public class OracleDbConnectionManagerTests
{
    /// <summary>
    /// Verifies that OracleConnection instances created by OracleDbConnectionManager
    /// have CommandTimeout set to 30 seconds by default.
    /// </summary>
    [TestMethod]
    public void Build_ShouldSetConnectionCommandTimeoutTo30Seconds()
    {
        // Arrange
        var manager = new TestableOracleDbConnectionManager();
        var connectionString = "Data Source=dummy;User Id=test;Password=test;";

        // Act - Call Build directly via reflection to avoid opening the connection
        var buildMethod = typeof(OracleDbConnectionManager).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance);
        var connection = buildMethod!.Invoke(manager, new object[] { connectionString }) as OracleConnection;

        // Assert
        connection.Should().NotBeNull();
        connection!.CommandTimeout.Should().Be(30, "CommandTimeout should be set to 30 seconds");

        // Cleanup
        connection.Dispose();
    }

    /// <summary>
    /// Verifies that OracleCommand objects created from an OracleConnection
    /// inherit the CommandTimeout value from the connection.
    /// </summary>
    [TestMethod]
    public void CreatedCommand_ShouldInheritCommandTimeoutFromConnection()
    {
        // Arrange
        var manager = new TestableOracleDbConnectionManager();
        var connectionString = "Data Source=dummy;User Id=test;Password=test;";

        // Act - Call Build directly via reflection to avoid opening the connection
        var buildMethod = typeof(OracleDbConnectionManager).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance);
        var connection = buildMethod!.Invoke(manager, new object[] { connectionString }) as OracleConnection;
        var command = connection!.CreateCommand();

        // Assert
        command.Should().NotBeNull();
        command.Should().BeOfType<OracleCommand>();
        command.CommandTimeout.Should().Be(30, "Command should inherit 30-second timeout from connection");

        // Cleanup
        command.Dispose();
        connection.Dispose();
    }

    /// <summary>
    /// Verifies that CommandTimeout can be overridden on individual commands.
    /// </summary>
    [TestMethod]
    public void CommandTimeout_CanBeOverriddenPerCommand()
    {
        // Arrange
        var manager = new TestableOracleDbConnectionManager();
        var connectionString = "Data Source=dummy;User Id=test;Password=test;";
        var customTimeout = 60;

        // Act - Call Build directly via reflection to avoid opening the connection
        var buildMethod = typeof(OracleDbConnectionManager).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance);
        var connection = buildMethod!.Invoke(manager, new object[] { connectionString }) as OracleConnection;
        var command = connection!.CreateCommand();
        command.CommandTimeout = customTimeout;

        // Assert
        command.CommandTimeout.Should().Be(customTimeout, "Command timeout should be overridable");

        // Cleanup
        command.Dispose();
        connection.Dispose();
    }

    /// <summary>
    /// Verifies that the connection's CommandTimeout can also be changed after creation.
    /// </summary>
    [TestMethod]
    public void ConnectionCommandTimeout_CanBeOverriddenAfterBuild()
    {
        // Arrange
        var manager = new TestableOracleDbConnectionManager();
        var connectionString = "Data Source=dummy;User Id=test;Password=test;";
        var customTimeout = 120;

        // Act - Call Build directly via reflection to avoid opening the connection
        var buildMethod = typeof(OracleDbConnectionManager).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance);
        var connection = buildMethod!.Invoke(manager, new object[] { connectionString }) as OracleConnection;
        connection!.CommandTimeout = customTimeout;
        var command = connection.CreateCommand();

        // Assert
        connection.CommandTimeout.Should().Be(customTimeout, "Connection timeout should be overridable");
        command.CommandTimeout.Should().Be(customTimeout, "Command should inherit updated connection timeout");

        // Cleanup
        command.Dispose();
        connection.Dispose();
    }

    /// <summary>
    /// Testable version of OracleDbConnectionManager for unit testing.
    /// </summary>
    private sealed class TestableOracleDbConnectionManager : OracleDbConnectionManager
    {
    }
}
