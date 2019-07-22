
using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Events;
using Ark.Tools.EventSourcing.DomainEventPublisher;
using Raven.Client.Documents;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Subscriptions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.EventSourcing.Store;
using NLog;

namespace Ark.Tools.EventSourcing.RavenDb
{
    public abstract class RavenDbAggregateEventProcessor<TAggregate> 
        : IDisposable
        where TAggregate : IAggregateRoot
	{
		private static Logger _logger = LogManager.GetCurrentClassLogger();

		private readonly IDocumentStore _store;
        private IAggregateEventHandlerActivator _handlerActivator;
        private Task _subscriptionWorkerTask;
        private CancellationTokenSource _tokenSource;
        private object _gate = new object();

        readonly ConcurrentDictionary<Type, MethodInfo> _dispatchMethods = new ConcurrentDictionary<Type, MethodInfo>();

        public RavenDbAggregateEventProcessor(IDocumentStore store, IAggregateEventHandlerActivator handlerActivator)
        {            
            _store = store;
            _handlerActivator = handlerActivator;

            AggregateName = AggregateHelper<TAggregate>.Name;            
        }

        protected abstract string UniqueProcessorName { get; }
        protected string AggregateName { get; }

        protected string SubscriptionName => $"{AggregateName}/{UniqueProcessorName}";

        public async Task StartAsync(CancellationToken ctk = default)
        {
            var prefix = AggregateName + "/";
            try {
                await _store.Subscriptions.CreateAsync(new SubscriptionCreationOptions
                {
                    Name = SubscriptionName,
                    Query = $@"from {RavenDbEventSourcingConstants.AggregateEventsCollectionName} as e
                               where startsWith(id(e), '{prefix}')
                    "
                });
            } catch (Exception e) when (e.Message.Contains("is already in use in a subscription with different Id"))
            {
            }

            lock (_gate)
            {
                if (_subscriptionWorkerTask != null)
                    throw new InvalidOperationException("Already started");


                _tokenSource = new CancellationTokenSource();
                _subscriptionWorkerTask = Task.Run(() => _run(_tokenSource.Token), ctk);
            }
        }

        private async Task _run(CancellationToken ctk = default)
        {
            while (!ctk.IsCancellationRequested)
            {
                try
                {
					using (var worker = _store.Subscriptions.GetSubscriptionWorker<AggregateEventStore>(
						new SubscriptionWorkerOptions(SubscriptionName)
						{
							Strategy = SubscriptionOpeningStrategy.WaitForFree,
							MaxDocsPerBatch = 100,
						}))
					{
						await worker.Run(_exec, ctk);
					}
                }
                catch (TaskCanceledException) { throw; }
                catch (Exception ex)
                {
					_logger.Warn(ex, $"Failed processing events for aggregate {AggregateName}");
                    await Task.Delay(TimeSpan.FromSeconds(5), ctk);
                }
            }
            
        }

        private async Task _exec(SubscriptionBatch<AggregateEventStore> batch)
        {
            var tasks = batch.Items.GroupBy(x => x.Result.AggregateId)
                .Select(async g =>
                {
                    foreach (var e in g)
                    {
                        AggregateEventEnvelope<TAggregate> envelope = e.Result.FromStore<TAggregate>();
                        var evtType = envelope.Event.GetType();
                        var methodToInvoke = _dispatchMethods
                            .GetOrAdd(evtType, type => _getDispatchMethod(evtType));

                        await (Task)methodToInvoke.Invoke(this, new object[] { envelope.Event, envelope.Metadata });
                    }
                });


            await Task.WhenAll(tasks);            
        }

        Task _invokeHandler<TEvent>(TEvent evt, IMetadata metadata)
            where TEvent : IAggregateEvent<TAggregate>
        {
            var handler = _handlerActivator.GetHandler<TAggregate, TEvent>(evt);

            if (handler != null)
                return handler.HandleAsync(evt, metadata, _tokenSource.Token);
            else
                return Task.CompletedTask;
        }

        class FakeEvent : IAggregateEvent<TAggregate> { }

        MethodInfo _getDispatchMethod(Type eventType)
        {
            Func<FakeEvent, IMetadata, Task> a = _invokeHandler<FakeEvent>;

            return a.Method.GetGenericMethodDefinition().MakeGenericMethod(eventType);
        }

        public async Task StopAsync()
        {
            Task runtask;
            lock (_gate)
            {
                _tokenSource.Cancel();
                _tokenSource = null;
                runtask = _subscriptionWorkerTask;
                _subscriptionWorkerTask = null;
            }

            try
            {
                await runtask;
            }
            catch (TaskCanceledException) { }
        }

        public void Dispose()
        {
            ((IDisposable)_worker)?.Dispose();
            _tokenSource?.Dispose();
        }
    }
}
