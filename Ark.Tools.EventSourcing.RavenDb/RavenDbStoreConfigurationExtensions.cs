using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Store;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations.Revisions;
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

                if (typeof(AggregateState<,>).IsAssignableFromEx(type))
                {
                    while (!(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AggregateState<,>)))
                        type = type.BaseType;

                    var aggregateName = AggregateHelper.AggregateName(type.GetGenericArguments()[0]);
                    return aggregateName;
                }

                return null;
            });

            store.Conventions.UseOptimisticConcurrency = true;
            store.Conventions.ThrowIfQueryPageSizeIsNotSet = true;

            return store;
        }

        public static DocumentConventions AddFindCollectionName(this DocumentConventions conventions, Func<Type, string> func)
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
                {
                    {"@Outbox", new RevisionsCollectionConfiguration {Disabled = true}},                    
                }
            }));
        }
    }
}
