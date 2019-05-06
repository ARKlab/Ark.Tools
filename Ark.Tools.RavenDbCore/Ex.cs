using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Client;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Documents.Smuggler;
using Raven.Client.Exceptions.Cluster;
using Raven.Client.Util;
using Raven.Embedded;
using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace Ark.Tools.RavenDb
{
	public static class Ex
	{
		public static void EnsureDatabaseExists(this IDocumentStore store, string database = null, bool createDatabaseIfNotExists = true)
		{
			database = database ?? store.Database;

			if (string.IsNullOrWhiteSpace(database))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(database));

			try
			{
				store.Maintenance.ForDatabase(database).Send(new GetStatisticsOperation());
			}
			catch (DatabaseDoesNotExistException)
			{
				if (createDatabaseIfNotExists == false)
					throw;

				try
				{
					store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(database)));
				}
				catch (ConcurrencyException)
				{
					// The database was already created before calling CreateDatabaseOperation
				}

			}
		}

		public static void ResetDatabaseWithName(this IDocumentStore store, string databaseName)
		{
			store.Maintenance.Server.Send(new DeleteDatabasesOperation(store.Database, hardDelete: true));

			string[] databaseNames;
			do
			{
				var operation = new GetDatabaseNamesOperation(0, 25);
				databaseNames = store.Maintenance.Server.Send(operation);
			} while (databaseNames.Contains(databaseName));

			EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions(databaseName)).EnsureDatabaseExists();
		}

		public static void WaitForIndexing(this IDocumentStore store, string databaseName = null, TimeSpan? timeout = null)
		{
			var admin = store.Maintenance.ForDatabase(databaseName);

			timeout = timeout ?? TimeSpan.FromMinutes(1);

			var sp = Stopwatch.StartNew();
			while (sp.Elapsed < timeout.Value)
			{
				var databaseStatistics = admin.Send(new GetStatisticsOperation());
				var indexes = databaseStatistics.Indexes
					.Where(x => x.State != IndexState.Disabled);

				if (indexes.All(x => x.IsStale == false
					&& x.Name.StartsWith(Constants.Documents.Indexing.SideBySideIndexNamePrefix) == false))
					return;

				if (databaseStatistics.Indexes.Any(x => x.State == IndexState.Error))
				{
					break;
				}

				Thread.Sleep(100);
			}

			var errors = admin.Send(new GetIndexErrorsOperation());

			string allIndexErrorsText = string.Empty;
			if (errors != null && errors.Length > 0)
			{
				var allIndexErrorsListText = string.Join("\r\n",
					errors.Select(FormatIndexErrors));
				allIndexErrorsText = $"Indexing errors:\r\n{ allIndexErrorsListText }";

				string FormatIndexErrors(IndexErrors indexErrors)
				{
					var errorsListText = string.Join("\r\n",
						indexErrors.Errors.Select(x => $"- {x}"));
					return $"Index '{indexErrors.Name}' ({indexErrors.Errors.Length} errors):\r\n{errorsListText}";
				}
			}

			throw new TimeoutException($"The indexes stayed stale for more than {timeout.Value}.{ allIndexErrorsText }");
		}
	}
}
