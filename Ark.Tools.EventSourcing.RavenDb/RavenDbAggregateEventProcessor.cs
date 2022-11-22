using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Events;
using Raven.Client.Documents;
using Raven.Client.Documents.Subscriptions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.EventSourcing.Store;
using NLog;
using Raven.Client.Exceptions.Database;
using Raven.Client.Exceptions.Documents.Subscriptions;
using Raven.Client.Exceptions.Security;

namespace Ark.Tools.EventSourcing.RavenDb
{
	public abstract class RavenDbAggregateEventProcessor<TAggregate>
		: IDisposable
		where TAggregate : IAggregate
	{
		private static Logger _logger = LogManager.GetCurrentClassLogger();

		private readonly IDocumentStore _store;
		private IAggregateEventHandlerActivator _handlerActivator;
		private Task? _subscriptionWorkerTask;
		private CancellationTokenSource? _tokenSource;
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
			try
			{
				await _store.Subscriptions.CreateAsync(new SubscriptionCreationOptions
				{
					Name = SubscriptionName,
					Query = $@"
from {RavenDbEventSourcingConstants.AggregateEventsCollectionName} as e
where startsWith(id(e), '{prefix}')
                "
				}, token:ctk);
			}
			catch (Exception e) when (e.Message.Contains("is already in use in a subscription with different Id"))
			{
			}

			lock (_gate)
			{
				if (_subscriptionWorkerTask != null)
					throw new InvalidOperationException("Already started");


				_tokenSource = new CancellationTokenSource();
				_subscriptionWorkerTask = Task.Factory.StartNew(() => _run(_tokenSource.Token), _tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			}
		}

		private async Task _run(CancellationToken ctk = default)
		{
			while (!ctk.IsCancellationRequested)
			{
				var options = new SubscriptionWorkerOptions(SubscriptionName)
				{
					Strategy = SubscriptionOpeningStrategy.TakeOver,
					MaxDocsPerBatch = 10,
					ReceiveBufferSizeInBytes = 25 * 1024 * 1024
				};

				// here we configure that we allow a down time of up to 2 hours, and will wait for 2 minutes for reconnecting
				options.MaxErroneousPeriod = TimeSpan.MaxValue;
				options.TimeToWaitBeforeConnectionRetry = TimeSpan.FromSeconds(10);

				var subscriptionWorker = _store.Subscriptions.GetSubscriptionWorker<AggregateEventStore>(options);

				try
				{
					// here we are able to be informed of any exception that happens during processing                    
					subscriptionWorker.OnSubscriptionConnectionRetry += ex =>
					{
						_logger.Error(ex, "Error during subscription processing: {SubscriptionName}", SubscriptionName);
					};

					_logger.Info("Start processing {SubscriptionName}", SubscriptionName);
					await subscriptionWorker.Run(_exec, ctk);

					// Run will complete normally if you have disposed the subscription
					return;
				}
				catch (Exception e)
				{
					_logger.Error(e, "Failure in subscription: " + SubscriptionName);

					if (e is DatabaseDoesNotExistException ||
						e is SubscriptionDoesNotExistException ||
						e is SubscriptionInvalidStateException ||
						e is AuthorizationException)
						throw; // not recoverable


					if (e is SubscriptionClosedException)
						// closed explicitly by admin, probably
						return;

					if (e is SubscriberErrorException se)
						throw;

					// handle this depending on subscription
					// open strategy (discussed later)
					if (e is SubscriptionInUseException)
					{
						await Task.Delay(TimeSpan.FromSeconds(30), ctk);
						continue;
					}

					continue;
				}
				finally
				{
					subscriptionWorker.Dispose();
				}
			}

		}

		private async Task _exec(SubscriptionBatch<AggregateEventStore> batch)
		{
			_logger.Trace($"Got a batch of {batch.NumberOfItemsInBatch} items");

			var tasks = batch.Items.GroupBy(x => x.Result.AggregateId)
				.Select(async g =>
				{
					foreach (var e in g)
					{
						AggregateEventEnvelope<TAggregate> envelope = e.Result.FromStore<TAggregate>();
						var evtType = envelope.Event.GetType();
						var methodToInvoke = _dispatchMethods
							.GetOrAdd(evtType, type => _getDispatchMethod(evtType));

						await (Task)methodToInvoke.Invoke(this, new object[] { envelope.Event, envelope.Metadata })!;
					}
				});


			await Task.WhenAll(tasks);
		}

		Task _invokeHandler<TEvent>(TEvent evt, IMetadata metadata)
			where TEvent : IAggregateEvent<TAggregate>
		{
			var handler = _handlerActivator.GetHandler<TAggregate, TEvent>(evt);

			if (handler != null)
				return handler.HandleAsync(evt, metadata, _tokenSource?.Token ?? default);
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
			Task? runtask;
			lock (_gate)
			{
				_tokenSource?.Cancel();
				_tokenSource = null;
				runtask = _subscriptionWorkerTask;
				_subscriptionWorkerTask = null;
			}

			try
			{
                if (runtask is not null)
				    await runtask;
			}
			catch (TaskCanceledException) { }
		}

		protected virtual void Dispose(bool disposing) 
		{ 
			if (disposing) _tokenSource?.Dispose();
		}

		public void Dispose()
		{
			Dispose(true);
            GC.SuppressFinalize(this);
		}
	}
}
