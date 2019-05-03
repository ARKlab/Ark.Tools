using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Embedded;
using System;
using System.Linq;

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
	}
}
