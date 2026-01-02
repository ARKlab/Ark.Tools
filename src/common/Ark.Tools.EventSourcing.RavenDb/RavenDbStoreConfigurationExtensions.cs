using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Store;

using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations.Revisions;
using Raven.Client.ServerWide.Operations;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.RavenDb
{
    public static class RavenDbStoreConfigurationExtensions
    {
        public static DocumentStore ConfigureForArkEventSourcing(this DocumentStore store)
        {
            var current = store.Conventions.FindCollectionName;
            store.Conventions.AddFindCollectionName(type =>
            {
                if (typeof(IOutboxEvent).IsAssignableFrom(type))
                    return RavenDbEventSourcingConstants.OutboxCollectionName;

                if (typeof(AggregateEventStore<,>).IsAssignableFromEx(type) || typeof(AggregateEventStore).IsAssignableFrom(type))
                {
                    return RavenDbEventSourcingConstants.AggregateEventsCollectionName;
                }

                return null;
            });

            store.Conventions.UseOptimisticConcurrency = true;

            return store;
        }

        public static DocumentConventions AddFindCollectionName(this DocumentConventions conventions, Func<Type, string?> func)
        {
            var current = conventions.FindCollectionName;
            conventions.FindCollectionName = type =>
            {
                return func?.Invoke(type)
                    ?? current?.Invoke(type)
                    ?? DocumentConventions.DefaultGetCollectionName(type);
            };

            return conventions;
        }

        public static async Task SetupArk(this IDocumentStore store)
        {
            await store.Maintenance.SendAsync(new ConfigureRevisionsOperation(new RevisionsConfiguration
            {
                Default = new RevisionsCollectionConfiguration
                {
                    Disabled = false,
                    PurgeOnDelete = false,
                    MinimumRevisionsToKeep = null,
                    MinimumRevisionAgeToKeep = null,
                },
                Collections = new Dictionary<string, RevisionsCollectionConfiguration>
(StringComparer.Ordinal)
                {
                    {RavenDbEventSourcingConstants.OutboxCollectionName, new RevisionsCollectionConfiguration {Disabled = true} },
                }
            })).ConfigureAwait(false);
        }

        public static async Task EnsureNoRevisionForAggregateState<TAggregate>(this IDocumentStore store)
            where TAggregate : IAggregate
        {
            var collection = AggregateHelper<TAggregate>.Name;
            var database = await store.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(store.Database)).ConfigureAwait(false);
            var revision = database.Revisions;
            if ((revision.Collections.ContainsKey(collection) && revision.Collections[collection].Disabled == true)
                || revision.Default.Disabled)
                return;

            revision.Collections.Add(collection, new RevisionsCollectionConfiguration
            {
                Disabled = true
            });

            await store.Maintenance.SendAsync(new ConfigureRevisionsOperation(revision)).ConfigureAwait(false);
        }
    }
}