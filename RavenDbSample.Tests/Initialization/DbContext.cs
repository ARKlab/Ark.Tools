using Ark.Tools.RavenDb;
using NLog;
using TechTalk.SpecFlow;
using Dapper;
using System.Data.SqlClient;
using System.Linq;
using Raven.Embedded;
using Raven.TestDriver;
using Raven.Client.Documents;
using Raven.Client.ServerWide.Operations;
using System.Diagnostics;

namespace RavenDBSample.Tests
{
	[Binding]
	public class DbContext
	{
		private static Logger _logger = LogManager.GetCurrentClassLogger();

		const string DatabaseConnectionString = "http://127.0.0.1:8080";

		private const string _databaseName = "RavenDb";

		[BeforeTestRun(Order = 0)]
		public static void TestEnvironmentInitialization()
		{
			EmbeddedServer.Instance.StartServer(new ServerOptions
			{
				ServerUrl = DatabaseConnectionString,
				
			});

			var store = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions(_databaseName));

			store.ResetDatabaseWithName(_databaseName);
		}

		[BeforeScenario]
		public void ResetDatabaseOnEachScenario(FeatureContext fctx)
		{
			//DocumentStore = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions("RavenDb"));
		}

		[BeforeScenario("DefaultConfigurations")]
		public void ResetConfigs()
		{
			//_logger.Info(@"ResetConfigs all Database");

			//using (var conn = new SqlConnection(NavisionMWConnectionString))
			//{
			//    conn.Execute(
			//        @"[core].[sp_ResetFull_onlyForTesting]",
			//        new
			//        {
			//            areYouReallySure = true,
			//            resetConfig = true,
			//            cleanHistory = true
			//        },
			//        commandType: System.Data.CommandType.StoredProcedure,
			//        commandTimeout: 60);
			//}
		}

		[AfterScenario]
		public void Boh(ScenarioContext ctx)
		{
			if (ctx.ScenarioExecutionStatus == ScenarioExecutionStatus.TestError && Debugger.IsAttached)
				Debugger.Break();
		}
	}
}