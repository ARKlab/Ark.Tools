using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

using Reqnroll;

using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Tests.Init
{
    [Binding]
    sealed class DatabaseUtils
    {
        public const string DatabaseConnectionString = @"Data Source=127.0.0.1;User Id=sa;Password=IntegrationTestsDbPassword85!;Pooling=True;Connect Timeout=60;Encrypt=True;TrustServerCertificate=True";

        [BeforeTestRun(Order = -1)]
        public static async Task CreateNLogDatabaseIfNotExists()
        {
            await using var conn = new SqlConnection(DatabaseConnectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand($"IF (db_id(N'Logs') IS NULL) BEGIN CREATE DATABASE [Logs] END;", conn);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        [BeforeTestRun(Order = -1)]
        public static void DeployDB()
        {
            var instance = new DacServices(DatabaseConnectionString);
            using var dacpac = DacPackage.Load("Ark.Reference.Core.Database.dacpac");
            instance.Deploy(dacpac, "Ark.Reference.Core.Database", true, new DacDeployOptions()
            {
                CreateNewDatabase = true,
                AllowIncompatiblePlatform = true // needed since Database project is AzureV12 and under tests 2022 is used
            });
        }

        [BeforeScenario]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Reqnroll requires instance methods for BeforeScenario")]
        public async Task CleanUpEntireDbBeforeScenario(FeatureContext fctx, ScenarioContext sctx)
        {
            if (fctx.FeatureInfo.Tags.Contains("CleanDbBeforeScenario", StringComparer.Ordinal) ||
                sctx.ScenarioInfo.Tags.Contains("CleanDbBeforeScenario", StringComparer.Ordinal))
            {
                if (fctx.FeatureInfo.Tags.Contains("CleanProfileCalendarBeforeScenario", StringComparer.Ordinal) || sctx.ScenarioInfo.Tags.Contains("CleanProfileCalendarBeforeScenario", StringComparer.Ordinal))
                    await _cleanUpEntireDb(resetProfileCalendar: true).ConfigureAwait(false);
                else
                    await _cleanUpEntireDb().ConfigureAwait(false);
            }
        }

        private static async Task _cleanUpEntireDb(bool resetProfileCalendar = false)
        {
            await using var ctx = new SqlConnection(TestHost.DBConfig.ConnectionString);
            await ctx.OpenAsync().ConfigureAwait(false);
            await using var tx = await ctx.BeginTransactionAsync().ConfigureAwait(false);

            await ctx.ExecuteAsync(
                @"[ops].[ResetFull_onlyForTesting]",
                new
                {
                    areYouReallySure = true,
                    resetConfig = true,
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 60,
                transaction: tx).ConfigureAwait(false);

            await tx.CommitAsync().ConfigureAwait(false);
        }
    }
}