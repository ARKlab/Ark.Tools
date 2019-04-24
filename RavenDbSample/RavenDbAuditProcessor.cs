using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Subscriptions;
using RavenDbSample.Auditable;
using RavenDbSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDbSample
{
	public sealed class RavenDbAuditProcessor : IHostedService, IDisposable
	{
		private readonly IDocumentStore _store;
		private List<SubscriptionWorker<Revision<IAuditable>>> _workers = new List<SubscriptionWorker<Revision<IAuditable>>>();
		private List<Task> _subscriptionWorkerTasks = new List<Task>();
		private CancellationTokenSource _tokenSource;
		private readonly object _gate = new object();
		private readonly HashSet<string> _names = new HashSet<string>();
		private const string _prefixName= "AuditProcessor/";

		public RavenDbAuditProcessor(IDocumentStore store/*, List<Type> types*/)
		{
			_store = store;

			var types = new List<Type>() { typeof(BaseOperation) };

			foreach (var t in types)
				_names.Add(store.Conventions.GetCollectionName(t));

			foreach (var name in _names)
			{
				_workers.Add(_store.Subscriptions.GetSubscriptionWorker<Revision<IAuditable>>(new SubscriptionWorkerOptions(_prefixName + name)
				{
					Strategy = SubscriptionOpeningStrategy.WaitForFree,
					MaxDocsPerBatch = 10,
				}));
			}
		}

		public async Task StartAsync(CancellationToken ctk = default)
		{
			foreach (var name in _names)
			{
				try
				{
					await _store.Subscriptions.CreateAsync(new SubscriptionCreationOptions()
					{
						Name = _prefixName + name,
						Query = $@"From {name} (Revisions = true)"
					});
				}
				catch (Exception e) when (e.Message.Contains("is already in use in a subscription with different Id"))
				{
				}
			}

			lock (_gate)
			{
				int i = 0;
				foreach (var worker in _workers)
				{
					if (_subscriptionWorkerTasks.Count > 0 && _subscriptionWorkerTasks[i] != null) //if (_subscriptionWorkerTask != null)
						throw new InvalidOperationException("Already started");

					_tokenSource = new CancellationTokenSource();
					_subscriptionWorkerTasks.Add(Task.Run(() => _run(worker, _tokenSource.Token), ctk));
					i++;
				}
			}
		}

		private async Task _run(SubscriptionWorker<Revision<IAuditable>> worker, CancellationToken ctk = default)
		{
			while (!ctk.IsCancellationRequested)
			{
				try
				{
					await worker.Run(_processAuditChange, ctk);
				}
				catch (TaskCanceledException) { throw; }
				catch (Exception)
				{
					// retry
				}
			}

		}

		private async Task _processAuditChange(SubscriptionBatch<Revision<IAuditable>> batch)
		{
			using (var session = batch.OpenAsyncSession())
			{
				foreach (var e in batch.Items)
				{
					session.Advanced.Defer(new PatchCommandData(
						id: e.Id,
						changeVector: null,
						patch: new PatchRequest
						{
							Script = "this.EntityChangeVector[args.Id] = args.EntityChangeVector",
							Values =
							{
								{
									"EntityChangeVector",
									new ChangeVectorDto
									{
										Prev = session.Advanced.GetChangeVectorFor(e.Result.Previous),
										Curr = session.Advanced.GetChangeVectorFor(e.Result.Current)
									}
								},
								{
									"Id", e.Id
								}
							}
						},
						patchIfMissing: null));
				}

				await session.SaveChangesAsync();
			}
		}

		public async Task StopAsync(CancellationToken ctk = default)
		{
			List<Task> runtask = new List<Task>();
			lock (_gate)
			{
				_tokenSource.Cancel();
				_tokenSource = null;
				runtask.AddRange(_subscriptionWorkerTasks);
				_subscriptionWorkerTasks.Clear();
			}

			try
			{
				await Task.WhenAll(runtask);
			}
			catch (TaskCanceledException) { }
		}

		public void Dispose()
		{
			((IDisposable)_workers)?.Dispose();
			_tokenSource?.Dispose();
		}
	}
}
