using Ark.Tools.RavenDb;
using NLog;
using TechTalk.SpecFlow;
using System.Diagnostics;
using Raven.Client.Documents;
using Raven.Embedded;
using Raven.Client.ServerWide.Operations;
using System.Linq;
using Raven.Client.Documents.Operations.Revisions;
using System;

namespace RavenDBSample.Tests
{
	[Binding]
	public class DbContext
	{
		private static Logger _logger = LogManager.GetCurrentClassLogger();

		private static IDocumentStore _documentStore;
		public static string DatabaseConnectionString { get; private set; } = "http://127.0.0.1:8080";
		//const string DatabaseConnectionString = "http://127.0.0.1:8080";
		private const string _databaseName = "RavenDb";


		[BeforeTestRun(Order = int.MinValue)]
		public static void TestEnvironmentInitialization()
		{
			Environment.SetEnvironmentVariable("CODE_COVERAGE_SESSION_NAME", null);
			EmbeddedServer.Instance.StartServer(new ServerOptions
			{
				FrameworkVersion = "2.2.8",
				ServerUrl = DatabaseConnectionString,
			});

			DatabaseConnectionString = EmbeddedServer.Instance.GetServerUriAsync().GetAwaiter().GetResult().ToString();
			var store = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions(_databaseName));

			store.Maintenance.Server.Send(new DeleteDatabasesOperation(store.Database, hardDelete: true));

			string[] databaseNames;
			do
			{
				var operation = new GetDatabaseNamesOperation(0, 25);
				databaseNames = store.Maintenance.Server.Send(operation);
			} while (databaseNames.Contains(store.Database));

			store.EnsureDatabaseExists(_databaseName, configureRecord: r =>
			{
				r.Revisions = new RevisionsConfiguration()
				{
					Default = new RevisionsCollectionConfiguration
					{
						Disabled = false,
						PurgeOnDelete = false,
						MinimumRevisionsToKeep = null,
						MinimumRevisionAgeToKeep = null,
					},
					Collections = new System.Collections.Generic.Dictionary<string, RevisionsCollectionConfiguration>
					{
						{ "@Outbox", new RevisionsCollectionConfiguration { Disabled = true }}
					}
				};
			});

			_documentStore = store;


			//EmbeddedServer.Instance.StartServer(new ServerOptions
			//{
			//	ServerUrl = DatabaseConnectionString,
			//});

			//_documentStore = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions(_databaseName));

			//_documentStore.DeleteDatabaseWithName(_databaseName);
		}

		//[BeforeScenario]
		//public void ResetDatabaseOnEachScenario(FeatureContext fctx)
		//{
		//	_documentStore.DeleteDatabaseWithName(_databaseName);
		//}

		[BeforeScenario]
		public void ResetDatabaseOnEachScenarioCollection()
		{
			_documentStore.DeleteCollection("BaseOperations");
			_documentStore.DeleteCollection("Audits");

			_documentStore.WaitForIndexing();
		}

		[AfterScenario]
		public void KeepStudioRunning(ScenarioContext ctx)
		{
			if (ctx.ScenarioExecutionStatus == ScenarioExecutionStatus.TestError && Debugger.IsAttached)
				Debugger.Break();
		}

		//[BeforeScenario("ResetSomething")]
		[When(@"I wait for Indexing on Database '(.*)'")]
		protected void IWaitForIndexingOnDatabase(string database = null)
		{
			TimeSpan? timeout = null;

			_documentStore.WaitForIndexing(database, timeout);
		}
	}
}