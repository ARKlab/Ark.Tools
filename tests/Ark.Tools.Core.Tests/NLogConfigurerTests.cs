// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Ark.Tools.NLog;

using NLog;

namespace Ark.Tools.Core.Tests;

/// <summary>Tests the default NLog target thresholds.</summary>
[TestClass]
[DoNotParallelize]
public class NLogConfigurerTests
{
    /// <summary>Verifies the database target threshold for each environment.</summary>
    [TestMethod]
    [DataRow("Production", "Warn", "Info")]
    [DataRow("Test", "Info", "Debug")]
    public void WithArkDefaultTargetsAndRules_UsesExpectedDatabaseThreshold(string environment, string expectedLevelName, string disabledLevelName)
    {
        var expectedLevel = LogLevel.FromString(expectedLevelName);
        var disabledLevel = LogLevel.FromString(disabledLevelName);
        var originalEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var originalConfiguration = LogManager.Configuration;

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);

            NLogConfigurer.For("NLogConfigurerTests")
                .WithArkDefaultTargetsAndRules(new NLogConfigurer.Config(
                    SQLConnectionString: "invalid",
                    EnableConsole: false,
                    Async: false))
                .Apply();

            var databaseRule = LogManager.Configuration!.LoggingRules
                .Single(static rule => rule.RuleName == $"{NLogConfigurer.DatabaseTarget}-*");

            databaseRule.IsLoggingEnabledForLevel(expectedLevel).Should().BeTrue();
            databaseRule.IsLoggingEnabledForLevel(disabledLevel).Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalEnvironment);
            LogManager.Configuration = originalConfiguration;
        }
    }
}
