using Rebus.Transport;

using Ark.Tools.Core;

namespace Ark.Tools.Outbox.Rebus;


internal sealed class RebusOutboxProcessor : RebusOutboxProcessorCore
{
    private readonly IOutboxContextFactory _outboxContextFactory;

    public RebusOutboxProcessor(int topMessagesToRetrieve, ITransport transport, IOutboxContextFactory outboxContextFactory)
        : base(topMessagesToRetrieve, transport)
    {
        _outboxContextFactory = outboxContextFactory;
    }

    protected override ValueTask<IOutboxContextCore> CreateContextAsync(CancellationToken ctk)
    {
        return ValueTask.FromResult<IOutboxContextCore>(_outboxContextFactory.Create());
    }

    protected override ValueTask CommitContextAsync(IOutboxContextCore context, CancellationToken ctk)
    {
        if (context is not IContext syncContext)
            throw new InvalidOperationException("The outbox context must implement IContext.");

        syncContext.Commit();
        return ValueTask.CompletedTask;
    }

    protected override ValueTask DisposeContextAsync(IOutboxContextCore context)
    {
        if (context is not IContext syncContext)
            throw new InvalidOperationException("The outbox context must implement IContext.");

        syncContext.Dispose();
        return ValueTask.CompletedTask;
    }
}