using Ark.Tools.RavenDb;
using NLog;
using TechTalk.SpecFlow;
using Dapper;
using System.Data.SqlClient;
using System.Linq;

using System.Diagnostics;
using System;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Documents.Smuggler;
using Raven.Client.Exceptions.Cluster;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Client.Util;
using Raven.Embedded;
using System.Threading;

namespace RavenDBSample.Tests
{
	[Binding]
	public class DbContext
	{
		private static Logger _logger = LogManager.GetCurrentClassLogger();

		private static IDocumentStore _documentStore;
		const string DatabaseConnectionString = "http://127.0.0.1:8080";
		private const string _databaseName = "RavenDb";


		[BeforeTestRun(Order = 0)]
		public static void TestEnvironmentInitialization()
		{
			EmbeddedServer.Instance.StartServer(new ServerOptions
			{
				ServerUrl = DatabaseConnectionString,
			});

			_documentStore = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions(_databaseName));

			_documentStore.DeleteDatabaseWithName(_databaseName);
		}

		[BeforeScenario]
		public void ResetDatabaseOnEachScenario(FeatureContext fctx)
		{
			_documentStore.DeleteDatabaseWithName(_databaseName);
		}

		[BeforeScenario]
		public void ResetDatabaseOnEachScenarioCollection(FeatureContext fctx)
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