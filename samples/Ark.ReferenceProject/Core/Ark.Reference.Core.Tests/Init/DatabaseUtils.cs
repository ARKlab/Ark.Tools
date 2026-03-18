using Dapper;

using Microsoft.Data.SqlClient;

using Reqnroll;

using System.Data;
using System.Diagnostics;

namespace Ark.Reference.Core.Tests.Init;

[Binding]
sealed class DatabaseUtils
{
    public const string DatabaseConnectionString = @"Data Source=127.0.0.1;User Id=sa;Password=IntegrationTestsDbPassword85!;Pooling=True;Connect Timeout=60;Encrypt=True;TrustServerCertificate=True";

    [BeforeTestRun(Order = -1)]
    public static async Task CreateNLogDatabaseIfNotExists()
    {
        var conn = new SqlConnection(DatabaseConnectionString);
        await using var _ = conn.ConfigureAwait(false);
        await conn.OpenAsync().ConfigureAwait(false);
        var cmd = new SqlCommand($"IF (db_id(N'Logs') IS NULL) BEGIN CREATE DATABASE [Logs] END;", conn);
        await using var _cmd = cmd.ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    [BeforeTestRun(Order = -1)]
    public static async Task DeployDB()
    {
        // Use sqlpackage CLI to deploy dacpac.
        // DacFx programmatic API is incompatible with Microsoft.Data.SqlClient v7
        // because SqlAuthenticationMethod was moved to Extensions.Abstractions assembly.
        var dacpacPath = Path.GetFullPath("Ark.Reference.Core.Database.dacpac");
        if (!File.Exists(dacpacPath))
            throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Dacpac not found at {0}", dacpacPath));

        var psi = new ProcessStartInfo("sqlpackage")
        {
            ArgumentList =
            {
                "/Action:Publish",
                string.Format(CultureInfo.InvariantCulture, "/SourceFile:{0}", dacpacPath),
                string.Format(CultureInfo.InvariantCulture, "/TargetConnectionString:{0}", DatabaseConnectionString),
                "/TargetDatabaseName:Ark.Reference.Core.Database",
                "/p:CreateNewDatabase=True",
                "/p:AllowIncompatiblePlatform=True",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start sqlpackage process");

        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "sqlpackage failed with exit code {0}.\nStdOut: {1}\nStdErr: {2}", process.ExitCode, stdout, stderr));
    }

    [BeforeScenario]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Reqnroll requires instance methods for BeforeScenario")]
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
        var ctx = new SqlConnection(TestHost.DBConfig.ConnectionString);
        await using var _ = ctx.ConfigureAwait(false);
        await ctx.OpenAsync().ConfigureAwait(false);
        var tx = await ctx.BeginTransactionAsync().ConfigureAwait(false);
        await using var _tx = tx.ConfigureAwait(false);
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