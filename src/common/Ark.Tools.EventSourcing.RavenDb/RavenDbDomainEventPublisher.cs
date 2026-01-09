using Ark.Tools.EventSourcing.DomainEventPublisher;
using Ark.Tools.EventSourcing.Store;

using Raven.Client.Documents;
using Raven.Client.Documents.Subscriptions;

using System;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing.RavenDb(net10.0)', Before:
namespace Ark.Tools.EventSourcing.RavenDb
{
    public sealed class RavenDbDomainEventPublisher : IDisposable
    {
        private readonly IDocumentStore _store;
        private readonly SubscriptionWorker<OutboxEvent> _worker;
        private readonly IDomainEventPublisher _publisher;
        private Task? _subscriptionWorkerTask;
        private CancellationTokenSource? _tokenSource;
        private readonly Lock _gate = new();

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
            try
            {
                await _store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<OutboxEvent>
                {
                    Name = "OutboxEventPublisher",
                }, token: ctk).ConfigureAwait(false);
            }
            catch (Exception e) when (e.Message.Contains("is already in use in a subscription with different Id", StringComparison.Ordinal))
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
                    await _worker.Run(_exec, ctk).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { throw; }
                catch (Exception)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ctk).ConfigureAwait(false);
                }
            }

        }

        private async Task _exec(SubscriptionBatch<OutboxEvent> batch)
        {
            using var session = batch.OpenAsyncSession();
            foreach (var e in batch.Items)
            {
                await _publisher.PublishAsync(e.Result).ConfigureAwait(false);
                session.Delete(e.Result);
            }

            await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            Task? runtask;
            CancellationTokenSource? tokenSource;
            lock (_gate)
            {
                tokenSource = _tokenSource;
                _tokenSource = null;
                runtask = _subscriptionWorkerTask;
                _subscriptionWorkerTask = null;
            }

            try
            {
                if (tokenSource is not null)
                {
                    await tokenSource.CancelAsync().ConfigureAwait(false);
                    tokenSource.Dispose();
                }
                if (runtask != null)
                    await runtask.ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
        }

        public void Dispose()
        {
            ((IDisposable)_worker)?.Dispose();
            _tokenSource?.Dispose();
        }
=======
namespace Ark.Tools.EventSourcing.RavenDb;

public sealed class RavenDbDomainEventPublisher : IDisposable
{
    private readonly IDocumentStore _store;
    private readonly SubscriptionWorker<OutboxEvent> _worker;
    private readonly IDomainEventPublisher _publisher;
    private Task? _subscriptionWorkerTask;
    private CancellationTokenSource? _tokenSource;
    private readonly Lock _gate = new();

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
        try
        {
            await _store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<OutboxEvent>
            {
                Name = "OutboxEventPublisher",
            }, token: ctk).ConfigureAwait(false);
        }
        catch (Exception e) when (e.Message.Contains("is already in use in a subscription with different Id", StringComparison.Ordinal))
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
                await _worker.Run(_exec, ctk).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { throw; }
            catch (Exception)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ctk).ConfigureAwait(false);
            }
        }

    }

    private async Task _exec(SubscriptionBatch<OutboxEvent> batch)
    {
        using var session = batch.OpenAsyncSession();
        foreach (var e in batch.Items)
        {
            await _publisher.PublishAsync(e.Result).ConfigureAwait(false);
            session.Delete(e.Result);
        }

        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        Task? runtask;
        CancellationTokenSource? tokenSource;
        lock (_gate)
        {
            tokenSource = _tokenSource;
            _tokenSource = null;
            runtask = _subscriptionWorkerTask;
            _subscriptionWorkerTask = null;
        }

        try
        {
            if (tokenSource is not null)
            {
                await tokenSource.CancelAsync().ConfigureAwait(false);
                tokenSource.Dispose();
            }
            if (runtask != null)
                await runtask.ConfigureAwait(false);
        }
        catch (TaskCanceledException) { }
    }

    public void Dispose()
    {
        ((IDisposable)_worker)?.Dispose();
        _tokenSource?.Dispose();
>>>>>>> After


namespace Ark.Tools.EventSourcing.RavenDb;

public sealed class RavenDbDomainEventPublisher : IDisposable
{
    private readonly IDocumentStore _store;
    private readonly SubscriptionWorker<OutboxEvent> _worker;
    private readonly IDomainEventPublisher _publisher;
    private Task? _subscriptionWorkerTask;
    private CancellationTokenSource? _tokenSource;
    private readonly Lock _gate = new();

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
        try
        {
            await _store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<OutboxEvent>
            {
                Name = "OutboxEventPublisher",
            }, token: ctk).ConfigureAwait(false);
        }
        catch (Exception e) when (e.Message.Contains("is already in use in a subscription with different Id", StringComparison.Ordinal))
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
                await _worker.Run(_exec, ctk).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { throw; }
            catch (Exception)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ctk).ConfigureAwait(false);
            }
        }

    }

    private async Task _exec(SubscriptionBatch<OutboxEvent> batch)
    {
        using var session = batch.OpenAsyncSession();
        foreach (var e in batch.Items)
        {
            await _publisher.PublishAsync(e.Result).ConfigureAwait(false);
            session.Delete(e.Result);
        }

        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        Task? runtask;
        CancellationTokenSource? tokenSource;
        lock (_gate)
        {
            tokenSource = _tokenSource;
            _tokenSource = null;
            runtask = _subscriptionWorkerTask;
            _subscriptionWorkerTask = null;
        }

        try
        {
            if (tokenSource is not null)
            {
                await tokenSource.CancelAsync().ConfigureAwait(false);
                tokenSource.Dispose();
            }
            if (runtask != null)
                await runtask.ConfigureAwait(false);
        }
        catch (TaskCanceledException) { }
    }

    public void Dispose()
    {
        ((IDisposable)_worker)?.Dispose();
        _tokenSource?.Dispose();
    }
}