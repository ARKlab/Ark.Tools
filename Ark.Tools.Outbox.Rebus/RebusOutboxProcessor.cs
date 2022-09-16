using Ark.Tools.Core;

using Rebus.Bus;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Transport;
using Rebus.Workers.ThreadPoolBased;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.Rebus
{
    internal class RebusOutboxProcessor : IRebusOutboxProcessor
    {
		private readonly int _topMessagesToRetrieve;
		private readonly ITransport _transport;
		private readonly IBackoffStrategy _backoffStrategy;
        private readonly IOutboxContextFactory _outboxContextFactory;
        private readonly CancellationTokenSource _busDisposalCancellationTokenSource = new CancellationTokenSource();
		private readonly ILog _log;
        private Task _task;

        public RebusOutboxProcessor(
			int topMessagesToRetrieve,
			ITransport transport,
			IBackoffStrategy backoffStrategy,
			IRebusLoggerFactory rebusLoggerFactory,
			IOutboxContextFactory outboxContextFactory)
		{
			_topMessagesToRetrieve = topMessagesToRetrieve;
			_transport = transport;
			_backoffStrategy = backoffStrategy;
			_outboxContextFactory = outboxContextFactory;
			_log = rebusLoggerFactory.GetLogger<RebusOutboxProcessor>();
		}

		public void Start()
        {
			_task = _processOutboxMessages(_busDisposalCancellationTokenSource.Token);
		}

        public void Stop()
        {
			_busDisposalCancellationTokenSource.Dispose();
			_task.GetAwaiter().GetResult(); // wait for batch to complete
		}

        private async Task _processOutboxMessages(CancellationToken ctk)
		{
			_log.Debug("Starting outbox messages processor");

			while (!ctk.IsCancellationRequested)
			{
				try
				{
					bool waitForMessages = true;
					using (var ctx = _outboxContextFactory.Create())
					{
						var messages = await ctx.PeekLockMessagesAsync(_topMessagesToRetrieve, ctk);
						if (messages.Any())
						{
							using (var rebusTransactionScope = new RebusTransactionScope())
							{
								foreach (var message in messages)
								{
									var destinationAddress = message.Headers[OutboxTransportDecorator._outboxRecepientHeader];
									message.Headers.Remove(OutboxTransportDecorator._outboxRecepientHeader);
									await _transport.Send(destinationAddress, new TransportMessage(message.Headers, message.Body),
										rebusTransactionScope.TransactionContext).WithCancellation(ctk);
								}
								await rebusTransactionScope.CompleteAsync().WithCancellation(ctk);
							}
							waitForMessages = false;
							_backoffStrategy.Reset();
						}
						ctx.Commit();
					}

					if (waitForMessages)
					{
						await _backoffStrategy.WaitNoMessageAsync(ctk);
					}
				}
				catch (OperationCanceledException) when (ctk.IsCancellationRequested)
				{
					// we're shutting down
				}
				catch (Exception exception)
				{
					_log.Error(exception, "Unhandled exception in outbox messages processor");
                    try
                    {
                        await _backoffStrategy.WaitErrorAsync(ctk);
                    }
                    catch (OperationCanceledException) when (ctk.IsCancellationRequested)
                    {
                        // we're shutting down
                    }
                }
			}

			_log.Debug("Outbox messages processor stopped");
		}
	}
}

