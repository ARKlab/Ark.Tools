using Rebus.Logging;
using Rebus.Transport;
using Rebus.Workers.ThreadPoolBased;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.Rebus
{
    internal sealed class RebusAsyncOutboxProcessor : RebusOutboxProcessorCore
    {
        private readonly IOutboxAsyncContextFactory _outboxAsyncContextFactory;

        public RebusAsyncOutboxProcessor(int topMessagesToRetrieve, ITransport transport, IBackoffStrategy backoffStrategy, IRebusLoggerFactory rebusLoggerFactory, IOutboxAsyncContextFactory outboxContextFactory)
            : base(topMessagesToRetrieve, transport, backoffStrategy, rebusLoggerFactory)
        {
            _outboxAsyncContextFactory = outboxContextFactory;
        }

        protected override async Task<bool> _loop(CancellationToken ctk)
        {
            bool waitForMessages = true;
            await using var ctx = await _outboxAsyncContextFactory.CreateAsync(ctk);
            
            waitForMessages = await _tryProcessMessages(ctx, ctk);
            await ctx.CommitAsync(ctk);
            
            return waitForMessages;
        }
    }
}

