using Ark.Tools.Core;

using Rebus.Bus;
using Rebus.Transport;

using System;
using System.Reactive.Disposables;

namespace Ark.Tools.Outbox.Rebus
{
    public static class BusExtensions
    {
        public static RebusTransactionScope Enlist(this RebusTransactionScope tx, IOutboxContext context)
        {
            tx.TransactionContext.Items.TryAdd(OutboxTransportDecorator._outboxContextItemsKey, context);
            return tx;
        }

        public static RebusTransactionScope Enlist(this IBus bus, IOutboxContext context)
        {
            var tx = new RebusTransactionScope();
            return tx.Enlist(context);
        }
    }
}
