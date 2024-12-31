using Rebus.Messages;
using Rebus.Transport;
using Rebus.Transport.InMem;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus
{
    [Obsolete("Use DrainableInMemTransport", true)]
    public class TestsInMemTransport : InMemTransport
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Testing Purpose")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0069:Non-constant static fields should not be visible", Justification = "Testing Purpose")]
        public static int InProcessMessageCount;

        public TestsInMemTransport(InMemNetwork network, string? inputQueueAddress)
            : base(network, inputQueueAddress)
        {
        }

        public override Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            context.OnAck(ctx =>
            {
                Interlocked.Decrement(ref InProcessMessageCount);
                return Task.CompletedTask;
            });

            return base.Receive(context, cancellationToken);
        }

        protected override async Task SendOutgoingMessages(IEnumerable<OutgoingTransportMessage> outgoingMessages, ITransactionContext context)
        {
            var cnt = outgoingMessages.Where(x => x.DestinationAddress != "error").Count();

            await base.SendOutgoingMessages(outgoingMessages, context).ConfigureAwait(false);

            Interlocked.Add(ref InProcessMessageCount, cnt);
        }
    }
}
