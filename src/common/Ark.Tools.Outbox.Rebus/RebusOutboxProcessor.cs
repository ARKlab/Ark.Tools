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
        ((IContext)context).Commit();
        return ValueTask.CompletedTask;
    }

    protected override ValueTask DisposeContextAsync(IOutboxContextCore context)
    {
        ((IContext)context).Dispose();
        return ValueTask.CompletedTask;
    }
}