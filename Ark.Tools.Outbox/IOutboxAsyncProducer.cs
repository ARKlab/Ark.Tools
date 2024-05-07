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
    public interface IOutboxAsyncService
    {
        Task PublishAsync<T>(IOutboxContextAsync ctx, T message, CancellationToken ctk = default) where T : class
#if NETSTANDARD2_1
            => PublishAsync(ctx, message, null, ctk)
#endif
            ;

        Task PublishAsync<T>(IOutboxContextAsync ctx, T message, IDictionary<string, string>? optionalHeaders = null, CancellationToken ctk = default) where T : class;
    }

    public interface IOutboxAsyncConsumer
    {
        Task StartAsync(CancellationToken ctk = default);
        Task StopAsync(CancellationToken ctk = default);

        Task ClearAsync(CancellationToken ctk = default);
        Task<int> CountAsync(CancellationToken ctk = default);
    }

    public abstract class OutboxAsyncConsumerBase : IOutboxAsyncConsumer, IAsyncDisposable
    {
        private readonly Func<IOutboxContextAsync> _outboxContextFactory;
        private CancellationTokenSource? _processorCT;
        private Task? _processorTask;
        private bool _disposedValue;

        public TimeSpan SleepInterval { get; set; } = TimeSpan.FromSeconds(1);
        public int BatchSize { get; set; } = 1000;

        public OutboxAsyncConsumerBase(Func<IOutboxContextAsync> contextFactory)
        {
            _outboxContextFactory = contextFactory;
        }

        public async Task ClearAsync(CancellationToken ctk = default)
        {
            await using var ctx = _outboxContextFactory();
            await ctx.ClearAsync(ctk);
            await ctx.CommitAysnc(ctk);
        }

        public async Task<int> CountAsync(CancellationToken ctk = default)
        {
            await using var ctx = _outboxContextFactory();
            var ret = await ctx.CountAsync(ctk);
            await ctx.CommitAysnc(ctk);
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
                    await using var ctx = _outboxContextFactory();
                    var messages = await ctx.PeekLockMessagesAsync(BatchSize, ctk);
                    await _processMessages(messages, ctk);
                    await ctx.CommitAysnc(ctk);
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
            if (_processorTask is not null)
                await Task.WhenAny(_processorTask, Task.Delay(Timeout.Infinite, ctk));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _processorCT?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public ValueTask DisposeAsync()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
            return default;
        }
    }
}
