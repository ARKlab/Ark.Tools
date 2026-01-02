using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Documents.Queries;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Ark.Tools.RavenDb
{
    public static class Ex
    {
        public static void EnsureDatabaseExists(this IDocumentStore store, string? database = null, bool createDatabaseIfNotExists = true, int replicationFactor = 3, Action<DatabaseRecord>? configureRecord = null)
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

                var record = new DatabaseRecord(database);
                configureRecord?.Invoke(record);

                try
                {
                    store.Maintenance.Server.Send(new CreateDatabaseOperation(record, replicationFactor));
                }
                catch (ConcurrencyException)
                {
                    // The database was already created before calling CreateDatabaseOperation
                }
            }
        }

        public static IDocumentStore DeleteDatabaseWithName(this IDocumentStore store, string database)
        {
            database = database ?? store.Database;

            store.Maintenance.Server.Send(new DeleteDatabasesOperation(database, hardDelete: true));

            string[] databaseNames;
            do
            {
                var operation = new GetDatabaseNamesOperation(0, 25);
                databaseNames = store.Maintenance.Server.Send(operation);
            } while (databaseNames.Contains(database, StringComparer.Ordinal));

            return store;
        }

        public static void DeleteCollection(this IDocumentStore store, string collectionName, int timeSpan = 15)
        {
            var operation = store
                .Operations
                .Send(new DeleteByQueryOperation(new IndexQuery
                {
                    Query = "from " + collectionName
                }));

            operation.WaitForCompletion(TimeSpan.FromSeconds(timeSpan));
        }

        public static void WaitForIndexing(this IDocumentStore store, string? databaseName = null, TimeSpan? timeout = null)
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
                    && x.Name.StartsWith(Constants.Documents.Indexing.SideBySideIndexNamePrefix, StringComparison.Ordinal) == false))
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
                allIndexErrorsText = $"Indexing errors:\r\n{allIndexErrorsListText}";

                static string FormatIndexErrors(IndexErrors indexErrors)
                {
                    var errorsListText = string.Join("\r\n",
                        indexErrors.Errors.Select(x => $"- {x}"));
                    return $"Index '{indexErrors.Name}' ({indexErrors.Errors.Length} errors):\r\n{errorsListText}";
                }
            }

            throw new TimeoutException($"The indexes stayed stale for more than {timeout.Value}.{allIndexErrorsText}");
        }
    }
}