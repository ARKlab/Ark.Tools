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

		public static IDocumentStore _documentStore;
		const string DatabaseConnectionString = "http://127.0.0.1:8080";

		//private static readonly Lazy<IDocumentStore> TestServerStore = new Lazy<IDocumentStore>(_runServer, LazyThreadSafetyMode.ExecutionAndPublication);

		private const string _databaseName = "RavenDb";

		//private static IDocumentStore _runServer()
		//{
		//	var options = new ServerOptions
		//	{
		//		ServerUrl = DatabaseConnectionString,
		//	};

		//	EmbeddedServer.Instance.StartServer(options);

		//	DocumentStore = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions(_databaseName));

		//	//var url = AsyncHelpers.RunSync(() => EmbeddedServer.Instance.GetServerUriAsync());

		//	//var store = new DocumentStore
		//	//{
		//	//	Urls = new[] { url.AbsoluteUri }
		//	//};

		//	//store.Initialize();

		//	return DocumentStore;
		//}

		[BeforeTestRun(Order = 0)]
		public static void TestEnvironmentInitialization()
		{
			EmbeddedServer.Instance.StartServer(new ServerOptions
			{
				ServerUrl = DatabaseConnectionString,
			});

			_documentStore = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions(_databaseName));

			_documentStore.ResetDatabaseWithName(_databaseName);
		}

		[BeforeScenario]
		public void ResetDatabaseOnEachScenario(FeatureContext fctx)
		{
			_documentStore.ResetDatabaseWithName(_databaseName);
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