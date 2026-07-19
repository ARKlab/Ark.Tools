// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.WebInterface;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

using Rebus.Transport.InMem;
using Reqnroll;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Provides an isolated public test host for one behavioral scenario.</summary>
public sealed class SampleTestContext : IDisposable
{
    private readonly IHost _host;

    /// <summary>Initializes a new instance of the <see cref="SampleTestContext"/> class.</summary>
    public SampleTestContext()
        : this(configureFallbackPolicy: true)
    {
    }

    /// <summary>Creates a test context without the fallback authorization policy.</summary>
    /// <returns>The configured test context.</returns>
    public static SampleTestContext WithoutFallbackPolicy()
    {
        return new SampleTestContext(configureFallbackPolicy: false);
    }

    private SampleTestContext(bool configureFallbackPolicy)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
        var useSqlStore = string.Equals(
            Environment.GetEnvironmentVariable("ARK_SAMPLE_SQL_TESTS"),
            "1",
            StringComparison.Ordinal);
        var container = SampleComposition.BuildContainer(
            new InMemNetwork(),
            useSqlStore: useSqlStore,
            connectionString: DatabaseHooks.ConnectionString);
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "IntegrationTests",
            })
            .Build();
        var startup = new SampleStartup(container, configuration, configureFallbackPolicy);
        _host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(startup.ConfigureServices)
                .Configure(startup.Configure))
            .Build();
#pragma warning disable MA0045, VSTHRD002 // Reqnroll requires a synchronously constructible binding context.
        _host.Start();
#pragma warning restore MA0045, VSTHRD002
        Client = _host.GetTestServer().CreateClient();
    }

    /// <summary>Creates and resets the sample SQL database for opt-in integration runs.</summary>
    [Binding]
    #pragma warning disable CA2100 // SQL is fixed schema setup; the only interpolated identifier is validated.
    public sealed class DatabaseHooks
    {
        /// <summary>Gets the SQL connection string used by the sample integration database.</summary>
        public static string ConnectionString =>
            Environment.GetEnvironmentVariable("ARK_SAMPLE_SQL_CONNECTION")
            ?? "Server=localhost,1433;Database=Ark.MediatorFramework.Sample;User Id=sa;******;TrustServerCertificate=True;Encrypt=False";

        /// <summary>Creates the sample schema when SQL integration tests are enabled.</summary>
        [BeforeTestRun(Order = -1)]
        public static void EnsureDatabase()
        {
            if (!SqlEnabled())
                return;

            var builder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = string.Empty,
            };
            using var dacpac = DacPackage.Load("Ark.MediatorFramework.Sample.Database.dacpac");
            var instance = new DacServices(builder.ConnectionString);
            instance.Deploy(
                dacpac,
                "Ark.MediatorFramework.Sample",
                upgradeExisting: true,
                new DacDeployOptions
                {
                    CreateNewDatabase = true,
                    AllowIncompatiblePlatform = true,
                });
        }

        /// <summary>Clears SQL state between scenarios when SQL integration tests are enabled.</summary>
        [BeforeScenario(Order = -1)]
        public static async Task ResetDatabaseAsync()
        {
            if (!SqlEnabled())
                return;

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "[ops].[ResetFull_OnlyForTesting]";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            var parameter = command.Parameters.Add("@areYouReallySure", System.Data.SqlDbType.Bit);
            parameter.Value = true;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static bool SqlEnabled()
        {
            return string.Equals(Environment.GetEnvironmentVariable("ARK_SAMPLE_SQL_TESTS"), "1", StringComparison.Ordinal);
        }

    }
#pragma warning restore CA2100

    /// <summary>Gets the HTTP client for the sample's public API.</summary>
    public HttpClient Client { get; }

    /// <summary>Creates a handler for an in-process gRPC client.</summary>
    public HttpMessageHandler CreateGrpcHandler()
    {
        return _host.GetTestServer().CreateHandler();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();
    }
}
