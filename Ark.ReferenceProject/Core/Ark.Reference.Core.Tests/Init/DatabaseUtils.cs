using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

using System;
using System.Data;
using System.Linq;

using TechTalk.SpecFlow;

namespace Ark.Reference.Core.Tests.Init
{
    [Binding]
    class DatabaseUtils
    {
        public const string DatabaseConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True";

        [BeforeTestRun(Order = -1)]
        public static void CreateNLogDatabaseIfNotExists()
        {
            using (var conn = new SqlConnection(DatabaseConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand($"IF (db_id(N'Logs') IS NULL) BEGIN CREATE DATABASE [Logs] END;", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        [BeforeTestRun(Order = -1)]
        public static void DeployDB()
        {
            var instance = new DacServices(DatabaseConnectionString);
            using (var dacpac = DacPackage.Load("Ark.Reference.Core.Database.dacpac"))
            {
                try
                {
                    instance.Deploy(dacpac, "Ark.Reference.Core.Database", true, new DacDeployOptions()
                    {
                        CreateNewDatabase = true,
                    });
                }
                catch(Exception ex)
                { 
                    var a = ex;
                }
            }
        }

        [BeforeScenario]
        public void CleanUpEntireDbBeforeScenario(FeatureContext fctx, ScenarioContext sctx)
        {
            if (fctx.FeatureInfo.Tags.Contains("CleanDbBeforeScenario") ||
                sctx.ScenarioInfo.Tags.Contains("CleanDbBeforeScenario"))
            {
                if (fctx.FeatureInfo.Tags.Contains("CleanProfileCalendarBeforeScenario") || sctx.ScenarioInfo.Tags.Contains("CleanProfileCalendarBeforeScenario"))
                    _cleanUpEntireDb(resetProfileCalendar: true);
                else
                    _cleanUpEntireDb();
            }
        }

        private void _cleanUpEntireDb(bool resetProfileCalendar = false)
        {
            using (var ctx = new SqlConnection(TestHost.DBConfig.SQLConnectionString))
            {
                ctx.Open();
                using (SqlTransaction tx = ctx.BeginTransaction())
                {

                    ctx.Execute(
                        @"[ops].[ResetFull_onlyForTesting]",
                        new
                        {
                            areYouReallySure = true,
                            resetConfig = true,
                        },
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 60,
                        transaction: tx);

                    tx.Commit();
                    ctx.Close();
                }
            }
        }
    }
}
