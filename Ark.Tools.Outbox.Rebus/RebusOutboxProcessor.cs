﻿using Rebus.Logging;
using Rebus.Transport;
using Rebus.Workers.ThreadPoolBased;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.Rebus
{

    internal sealed class RebusOutboxProcessor : RebusOutboxProcessorCore
    {
        private readonly IOutboxContextFactory _outboxContextFactory;

        public RebusOutboxProcessor(int topMessagesToRetrieve, ITransport transport, IBackoffStrategy backoffStrategy, IRebusLoggerFactory rebusLoggerFactory, IOutboxContextFactory outboxContextFactory) 
            : base(topMessagesToRetrieve, transport, backoffStrategy, rebusLoggerFactory)
        {
            _outboxContextFactory = outboxContextFactory;
        }

        protected override async Task<bool> _loop(CancellationToken ctk)
        {
            bool waitForMessages = true;
            using (var ctx = _outboxContextFactory.Create())
            {
                waitForMessages = await _tryProcessMessages(ctx, ctk).ConfigureAwait(false);
                ctx.Commit();
            }

            return waitForMessages;
        }
    }
}

