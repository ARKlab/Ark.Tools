using Ark.Tools.Core;

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
    internal abstract class RebusOutboxProcessorCore : IRebusOutboxProcessor, IDisposable
    {
		private readonly IBackoffStrategy _backoffStrategy;
        private readonly int _topMessagesToRetrieve;
        private readonly ITransport _transport;
        private readonly CancellationTokenSource _busDisposalCancellationTokenSource = new CancellationTokenSource();
		private readonly ILog _log;
        private Task _task = Task.CompletedTask;
        private bool _disposedValue;

        protected RebusOutboxProcessorCore(
            int topMessagesToRetrieve, 
            ITransport transport,
            IBackoffStrategy backoffStrategy,
			IRebusLoggerFactory rebusLoggerFactory)
		{
			_backoffStrategy = backoffStrategy;
            _topMessagesToRetrieve = topMessagesToRetrieve;
            _transport = transport;
            _log = rebusLoggerFactory.GetLogger<RebusOutboxProcessorCore>();
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

        protected async Task<bool> _tryProcessMessages(IOutboxContextCore ctx, CancellationToken ctk)
        {
            bool waitForMessages = true;
            var messages = await ctx.PeekLockMessagesAsync(_topMessagesToRetrieve, ctk);
            if (messages.Any())
            {
                using (var rebusTransactionScope = new RebusTransactionScope())
                {
                    foreach (var message in messages)
                    {
                        var destinationAddress = message?.Headers?[OutboxTransportDecorator._outboxRecepientHeader];
                        message?.Headers?.Remove(OutboxTransportDecorator._outboxRecepientHeader);
                        await _transport.Send(destinationAddress!, new TransportMessage(message?.Headers, message?.Body),
                            rebusTransactionScope.TransactionContext).WithCancellation(ctk);
                    }
                    await rebusTransactionScope.CompleteAsync().WithCancellation(ctk);
                }
                waitForMessages = false;
            }

            return waitForMessages;
        }

        private async Task _processOutboxMessages(CancellationToken ctk)
		{
			_log.Debug("Starting outbox messages processor");

			while (!ctk.IsCancellationRequested)
			{
				try
                {
                    bool waitForMessages = await _loop(ctk);

                    if (waitForMessages)
                    {
                        await _backoffStrategy.WaitNoMessageAsync(ctk);
                    } else
                    {
                        _backoffStrategy.Reset();
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

        protected abstract Task<bool> _loop(CancellationToken ctk);


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _busDisposalCancellationTokenSource?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

