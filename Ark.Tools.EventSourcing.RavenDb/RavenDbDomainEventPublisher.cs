using Ark.Tools.EventSourcing.DomainEventPublisher;
using Ark.Tools.EventSourcing.Store;
using Raven.Client.Documents;
using Raven.Client.Documents.Subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.RavenDb
{
    public sealed class RavenDbDomainEventPublisher : IDisposable
    {
        private readonly IDocumentStore _store;
        private SubscriptionWorker<OutboxEvent> _worker;
        private readonly IDomainEventPublisher _publisher;
        private Task _subscriptionWorkerTask;
        private CancellationTokenSource _tokenSource;
        private object _gate = new object();

        public RavenDbDomainEventPublisher(IDocumentStore store, IDomainEventPublisher publisher)
        {
            _store = store;
            _worker = _store.Subscriptions.GetSubscriptionWorker<OutboxEvent>(new SubscriptionWorkerOptions("OutboxEventPublisher")
            {
                Strategy = SubscriptionOpeningStrategy.WaitForFree,
                MaxDocsPerBatch = 10,
            });
            _publisher = publisher;
        }

        public async Task StartAsync(CancellationToken ctk = default)
        {
            try {
                await _store.Subscriptions.CreateAsync<OutboxEvent>(new SubscriptionCreationOptions<OutboxEvent>
                {
                    Name = "OutboxEventPublisher",
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
                    await _worker.Run(_exec, ctk);
                }
                catch (TaskCanceledException) { throw; }
                catch (Exception)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ctk);
                }
            }
            
        }

        private async Task _exec(SubscriptionBatch<OutboxEvent> batch)
        {
            using (var session = batch.OpenAsyncSession())
            {
                foreach (var e in batch.Items)
                {
                    await _publisher.PublishAsync(e.Result);
                    session.Delete(e.Result);
                }

                await session.SaveChangesAsync();
            }                
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
