using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox
{
    /// <summary>
    /// Interface for a message Producer used by Application to submit messages to the Outbox
    /// </summary>
    public interface IOutboxService
    {
        Task PublishAsync<T>(IOutboxContext ctx, T message, CancellationToken ctk = default) where T : class
#if NETSTANDARD2_1
            => PublishAsync(ctx, message, null, ctk)
#endif
            ;

        Task PublishAsync<T>(IOutboxContext ctx, T message, IDictionary<string, string> optionalHeaders = null, CancellationToken ctk = default) where T : class;
    }

    public interface IOutboxConsumer
    {
        Task StartAsync(CancellationToken ctk = default);
        Task StopAsync(CancellationToken ctk= default);

        Task ClearAsync(CancellationToken ctk = default);
        Task<int> CountAsync(CancellationToken ctk = default);
    }

    public abstract class OutboxConsumerBase : IOutboxConsumer
    {
        private readonly Func<IOutboxContext> _outboxContextFactory;
        private CancellationTokenSource _processorCT;
        private Task _processorTask;

        public TimeSpan SleepInterval { get; set; } = TimeSpan.FromSeconds(1);
        public int BatchSize { get; set; } = 1000;

        public OutboxConsumerBase(Func<IOutboxContext> contextFactory)
        {
            _outboxContextFactory = contextFactory;
        }

        public async Task ClearAsync(CancellationToken ctk = default)
        {
            using var ctx = _outboxContextFactory();
            await ctx.ClearAsync(ctk);
            ctx.Commit();           
        }

        public async Task<int> CountAsync(CancellationToken ctk = default)
        {
            using var ctx = _outboxContextFactory();
            var ret = await ctx.CountAsync(ctk);
            ctx.Commit();
            return ret;
        }

        public Task StartAsync(CancellationToken ctk)
        {
            if (_processorCT != null) throw new InvalidOperationException("Consumer is already Started");

            _processorCT = new CancellationTokenSource();
            _processorTask = _loop(_processorCT.Token);

            if (_processorTask.IsCompleted)
                return _processorTask; // propagate startup errors

            return Task.CompletedTask;
        }

        private async Task _loop(CancellationToken ctk)
        {
            while (!ctk.IsCancellationRequested)
            {
                try
                {
                    using var ctx = _outboxContextFactory();
                    var messages = await ctx.PeekLockMessagesAsync(BatchSize, ctk);
                    await _processMessages(messages, ctk);
                    ctx.Commit();                    
                }
                catch (Exception e) when (!(e is TaskCanceledException))
                {
                    // chomp exceptions and retry
                    // TODO trace log
                }
                await Task.Delay(SleepInterval, ctk);
            }
        }

        protected virtual async Task _processMessages(IEnumerable<OutboxMessage> messages, CancellationToken ctk)
        {
            foreach (var m in messages)
                await _processMessage(m, ctk);
        }

        protected abstract Task _processMessage(OutboxMessage m, CancellationToken ctk);

        public async Task StopAsync(CancellationToken ctk)
        {
            _processorCT?.Dispose();
            await Task.WhenAny(_processorTask, Task.Delay(Timeout.Infinite, ctk));
        }
    }
}
