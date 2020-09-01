using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Store;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.CompareExchange;
using Raven.Client.Documents.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.RavenDb
{
    public class RavenDbEventSourcingAggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>
        : AggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        private readonly IAsyncDocumentSession _session;
        private readonly string _chexKey;
        private CompareExchangeValue<long> _chex;

        public RavenDbEventSourcingAggregateTransaction(
            IAsyncDocumentSession session,
            string aggregateId,
            IAggregateRootFactory aggregateRootFactory
            )
            : base(aggregateId, aggregateRootFactory)
        {
            session.Advanced.UseOptimisticConcurrency = false;
            _session = session;
            
            _chexKey = $"{AggregateHelper<TAggregate>.Name}/{aggregateId}/version";
        }


		public override async Task<IEnumerable<AggregateEventEnvelope<TAggregate>>> LoadHistory(long maxVersion, CancellationToken ctk = default)
		{
			var aggrname = AggregateHelper<TAggregate>.Name;
			var envelopes = new List<AggregateEventStore>();

			_chex = await _session.Advanced.ClusterTransaction.GetCompareExchangeValueAsync<long>(_chexKey, ctk);

			if (_chex == null)
			{
				return new AggregateEventEnvelope<TAggregate>[0];
			}

			maxVersion = Math.Min(_chex.Value, maxVersion);

			string lastId = null;
			while (envelopes.Count != maxVersion)
			{
				var results = await _session.Advanced.StreamAsync<AggregateEventStore>(
					$"{aggrname}/{Identifier}/",
					startAfter: lastId,
					pageSize: (int)maxVersion - envelopes.Count,
					token: ctk);

				while (envelopes.Count != maxVersion && await results.MoveNextAsync())
				{
					var envelope = results.Current;
					if (envelope.Document.AggregateVersion != envelopes.Count + 1)
						break;

					envelopes.Add(envelope.Document);
					lastId = envelope.Id;
				}
			}

			return envelopes.Select(x => x.FromStore<TAggregate>());
		}


		public override async Task SaveChangesAsync(CancellationToken ctk = default)
        {
            if (Aggregate.UncommittedAggregateEvents.Any())
            {
                if (_chex == null)
                    _chex = _session.Advanced.ClusterTransaction.CreateCompareExchangeValue<long>(_chexKey, Aggregate.Version);
                else
                    _chex.Value = Aggregate.Version;

                foreach (var e in Aggregate.UncommittedDomainEvents)
                {
                    var eventType = e.Event.GetType();
                    var outboxType = typeof(OutboxEvent<>).MakeGenericType(eventType);

                    var evt = (OutboxEvent)Activator.CreateInstance(outboxType);

                    evt.Id = e.Metadata.EventId;
                    evt.Metadata = e.Metadata.Values.ToDictionary(x => x.Key, x => x.Value);
                    evt.SetEvent(e.Event);

                    await _session.StoreAsync(evt, ctk);
                }

                foreach (var e in Aggregate.UncommittedAggregateEvents)
                {
                    await _session.StoreAsync(e.ToStore(), ctk);
                }

                await _session.StoreAsync(Aggregate.State, AggregateHelper<TAggregate>.Name + "/" + Aggregate.Identifier, ctk);

                await _session.SaveChangesAsync(ctk);
            }

            Aggregate.Commit();
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _session?.Dispose();
                }
                _disposedValue = true;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
