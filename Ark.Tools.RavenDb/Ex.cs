using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Client;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Embedded;
using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using Raven.Client.Documents.Linq;
using Microsoft.AspNet.OData.Query;
using Raven.Client.Documents.Session;
using Ark.Tools.Core;

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

		public static async Task<PagedResult<T>> GetPagedWithODataOptions<T>(
			this IRavenQueryable<T> query
			, ODataQueryOptions<T> options
			, ODataValidationSettings validations = null
			, int defaultPageSize = 100
			, CancellationToken ctk = default)
		where T : class
		{
			query.Statistics(out QueryStatistics stats);

			var settings = new ODataQuerySettings
			{
				HandleNullPropagation = HandleNullPropagationOption.False
			};

			//Validations
			options.Validate(validations ?? new RavenDefaultODataValidationSettings());

			//Query
			query = (options.Filter?.ApplyTo(query, settings) ?? query) as IRavenQueryable<T>;
			query = (options.OrderBy?.ApplyTo(query, settings) ?? query) as IRavenQueryable<T>;
			query = query.Skip(options.Skip?.Value ?? 0).Take(options.Top?.Value ?? defaultPageSize);

			var data = await query.ToListAsync();

			return new PagedResult<T>
			{
				Count = stats.TotalResults,
				Data = data,
				IsCountPartial = false,
				Limit = options.Top?.Value ?? defaultPageSize,
				Skip = options.Skip?.Value ?? 0
			};
		}
	}
}
