using Rebus.Logging;
using Rebus.Transport;
using Rebus.Workers.ThreadPoolBased;

using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Outbox.Rebus(net10.0)', Before:
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
            var ctx = await _outboxAsyncContextFactory.CreateAsync(ctk).ConfigureAwait(false);
            await using (ctx.ConfigureAwait(false))
            {
                waitForMessages = await _tryProcessMessages(ctx, ctk).ConfigureAwait(false);
                await ctx.CommitAsync(ctk).ConfigureAwait(false);

                return waitForMessages;
            }
=======
namespace Ark.Tools.Outbox.Rebus;

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
        var ctx = await _outboxAsyncContextFactory.CreateAsync(ctk).ConfigureAwait(false);
        await using (ctx.ConfigureAwait(false))
        {
            waitForMessages = await _tryProcessMessages(ctx, ctk).ConfigureAwait(false);
            await ctx.CommitAsync(ctk).ConfigureAwait(false);

            return waitForMessages;
>>>>>>> After


namespace Ark.Tools.Outbox.Rebus;

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
            var ctx = await _outboxAsyncContextFactory.CreateAsync(ctk).ConfigureAwait(false);
            await using (ctx.ConfigureAwait(false))
            {
                waitForMessages = await _tryProcessMessages(ctx, ctk).ConfigureAwait(false);
                await ctx.CommitAsync(ctk).ConfigureAwait(false);

                return waitForMessages;
            }
        }
    }