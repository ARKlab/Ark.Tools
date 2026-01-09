using Rebus.Bus;
using Rebus.Transport;

namespace Ark.Tools.Outbox.Rebus;

public static class BusExtensions
{
    public static RebusTransactionScope Enlist(this RebusTransactionScope tx, IOutboxContextCore context)
    {
        tx.TransactionContext.Items.TryAdd(OutboxTransportDecorator._outboxContextItemsKey, context);
        return tx;
    }

    public static RebusTransactionScope Enlist(this IBus bus, IOutboxContextCore context)
    {
        var current = AmbientTransactionContext.Current;
        var scope = new RebusTransactionScope();
        try
        {
            scope.Enlist(context);
            /* Copy all Items from existing TransactionContext, if any.
             * This is needed because from Rebus library, the RebusTransactionScope shouldn't be used inside a MessageHandler.
             * But Application Code do not know if is inside a MessageHandler or not...
             * 
             * Also, we cannot reuse the existing TransactionContext (when in an MessageHandler) because 'Complete()' 
             * on the TransactionContext would happens after the Application Transaction Commi().
             * 
             * By all means, we want a 'nested' TransactionContext.
             * 
             * We discovered this issue because the CorrelationIdFlowStep wasn't picking up the IncomingContext via the TransactionContext.
             * Cloning the Items in a 'nested' scope should support all cases. We don't clone 'handlers' ofc.
             */
            if (current != null)
            {
                foreach (var i in current.Items)
                {
                    scope.TransactionContext.Items.TryAdd(i.Key, i.Value);
                }
            }

            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
