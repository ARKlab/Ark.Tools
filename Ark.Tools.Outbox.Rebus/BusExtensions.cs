using Ark.Tools.Core;

using Rebus.Bus;
using Rebus.Transport;

using System;

namespace Ark.Tools.Outbox.Rebus
{
    public static class BusExtensions
    {
        public static IAsyncDisposable OutboxScope(this IBus bus, IOutboxContext context)
        {
            var tx = new RebusTransactionScope();
            tx.TransactionContext.Items.TryAdd(OutboxTransportDecorator._outboxContextItemsKey, context);

            return AsyncDisposable.Create(async () => { await tx.CompleteAsync(); tx.Dispose(); });
        }

        public static IOutboxContext EnlistInto(this IOutboxContext context, RebusTransactionScope tx)
        {
            tx.TransactionContext.Items.TryAdd(OutboxTransportDecorator._outboxContextItemsKey, context);
            return context;
        }

        public static RebusTransactionScope Enlist(this RebusTransactionScope tx, IOutboxContext context)
        {
            tx.TransactionContext.Items.TryAdd(OutboxTransportDecorator._outboxContextItemsKey, context);
            return tx;
        }
    }
}
