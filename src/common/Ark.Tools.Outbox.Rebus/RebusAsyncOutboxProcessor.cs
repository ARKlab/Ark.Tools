using Rebus.Transport;

using Ark.Tools.Core;

namespace Ark.Tools.Outbox.Rebus;

internal sealed class RebusAsyncOutboxProcessor : RebusOutboxProcessorCore
{
    private readonly IOutboxAsyncContextFactory _outboxAsyncContextFactory;

    public RebusAsyncOutboxProcessor(int topMessagesToRetrieve, ITransport transport, IOutboxAsyncContextFactory outboxContextFactory)
        : base(topMessagesToRetrieve, transport)
    {
        _outboxAsyncContextFactory = outboxContextFactory;
    }

    protected override async ValueTask<IOutboxContextCore> CreateContextAsync(CancellationToken ctk)
    {
        return await _outboxAsyncContextFactory.CreateAsync(ctk).ConfigureAwait(false);
    }

    protected override async ValueTask CommitContextAsync(IOutboxContextCore context, CancellationToken ctk)
    {
        await ((IAsyncContext)context).CommitAsync(ctk).ConfigureAwait(false);
    }

    protected override async ValueTask DisposeContextAsync(IOutboxContextCore context)
    {
        await ((IAsyncContext)context).DisposeAsync().ConfigureAwait(false);
    }
}