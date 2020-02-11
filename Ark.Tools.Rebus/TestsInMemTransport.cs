using Rebus.Messages;
using Rebus.Transport;
using Rebus.Transport.InMem;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus
{
	public class TestsInMemTransport : InMemTransportCopy
    {
        public static int InProcessMessageCount;
        public TestsInMemTransport(InMemNetwork network, string inputQueueAddress)
            : base(network, inputQueueAddress)
        {
        }

        public override Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            context.OnCompleted(() =>
            {
                Interlocked.Decrement(ref InProcessMessageCount);
                return Task.CompletedTask;
            });

            return base.Receive(context, cancellationToken);
        }

        protected override async Task SendOutgoingMessages(IEnumerable<OutgoingMessage> outgoingMessages, ITransactionContext context)
        {
            var cnt = outgoingMessages.Where(x => x.DestinationAddress != "error").Count();

            await base.SendOutgoingMessages(outgoingMessages, context);

            Interlocked.Add(ref InProcessMessageCount, cnt);
        }
    }
}
