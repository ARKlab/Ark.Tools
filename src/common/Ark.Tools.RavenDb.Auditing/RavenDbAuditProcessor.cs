using Microsoft.Extensions.Hosting;

using Raven.Client.Documents;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Subscriptions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.RavenDb.Auditing
{
    public sealed class RavenDbAuditProcessor : IHostedService, IDisposable
    {
        private readonly IDocumentStore _store;
        private readonly List<Task> _subscriptionWorkerTasks = new();
        private CancellationTokenSource? _tokenSource;
        private readonly Lock _gate = new();
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);
        private const string _prefixName = "AuditProcessor";

        public RavenDbAuditProcessor(IDocumentStore store, IAuditableTypeProvider provider)
        {
            _store = store;

            foreach (var t in provider.TypeList)
                _names.Add(store.Conventions.GetCollectionName(t));

        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var name in _names)
            {
                try
                {
                    var localName = await _store.Subscriptions.CreateAsync(new SubscriptionCreationOptions()
                    {
                        Name = _prefixName + name,
                        Query = $@"From {name}(Revisions = true)"
                    }, token: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (e.Message.Contains("is already in use in a subscription with different Id", StringComparison.Ordinal))
                {
                }
            }

            lock (_gate)
            {
                if (_subscriptionWorkerTasks.Count > 0)
                    throw new InvalidOperationException("Already started");

                _tokenSource = new CancellationTokenSource();

                foreach (var name in _names)
                {
                    _subscriptionWorkerTasks.Add(Task.Run(() => _run(name, _tokenSource.Token), cancellationToken));
                }
            }
        }

        private async Task _run(string name, CancellationToken ctk = default)
        {
            int retryCount = 0;

            while (!ctk.IsCancellationRequested)
            {
                try
                {
                    retryCount++;
                    var worker = _store.Subscriptions.GetSubscriptionWorker<Revision<dynamic>>(
                    new SubscriptionWorkerOptions(_prefixName + name)
                    {
                        Strategy = SubscriptionOpeningStrategy.WaitForFree,
                        MaxDocsPerBatch = 10,
                    });
                    await using (worker.ConfigureAwait(false))
                    {
                        await worker.Run(_processAuditChange, ctk).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException) { throw; }
                catch (Exception)
                {
                    if (retryCount > 10)
                        throw new InvalidOperationException($"Task Process for pachting records failed after {retryCount - 1} times");

                    // retry
                }
            }

        }

        private async Task _processAuditChange(SubscriptionBatch<Revision<dynamic>> batch)
        {
            using var session = _store.OpenAsyncSession();
            foreach (var e in batch.Items)
            {
                if (e.Result?.Current?.AuditId != null) //Delete does not have an audit 
                {
                    string? operation = default;

                    if (e.Result.Previous != null && e.Result.Current == null)
                        operation = nameof(Operations.Delete);
                    else if (e.Result.Previous != null && e.Result.Current != null)
                        operation = nameof(Operations.Update);
                    else if (e.Result.Previous == null && e.Result.Current != null)
                        operation = nameof(Operations.Insert);

                    session.Advanced.Defer(new PatchCommandData(
                        id: (string?)e.Result?.Current?.AuditId,
                        changeVector: null,
                        patch: new PatchRequest
                        {
                            Script = @"this.EntityInfo
											.forEach(eInfo => { 
												if (eInfo.EntityId == args.Id)
												{
													eInfo.CurrChangeVector = args.Cv; 
													eInfo.Operation = args.Operation;
													eInfo.LastModified = args.LastMod;
												}
											});
										 ",
                            Values =
                            {
                                    {
                                        "Cv", e.ChangeVector
                                    },
                                    {
                                        "Id", e.Id
                                    },
                                    {
                                        "LastMod", e.Metadata["@last-modified"]
                                    },
                                    {
                                        "Operation",  operation
                                    }
                            }
                        },
                        patchIfMissing: null));
                }
            }

            await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            List<Task> runtask = new();
            CancellationTokenSource? tokenSource = null;
            lock (_gate)
            {
                runtask.AddRange(_subscriptionWorkerTasks);
                _subscriptionWorkerTasks.Clear();
                tokenSource = _tokenSource;
                _tokenSource = null;
            }

            try
            {
                if (tokenSource is not null)
                {
                    await tokenSource.CancelAsync().ConfigureAwait(false);
                }
                await Task.WhenAll(runtask).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
        }

        public void Dispose()
        {
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
        }
    }
}