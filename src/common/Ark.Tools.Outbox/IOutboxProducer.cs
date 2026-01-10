
namespace Ark.Tools.Outbox;

/// <summary>
/// Interface for a message Producer used by Application to submit messages to the Outbox
/// </summary>
public interface IOutboxService
{
    Task PublishAsync<T>(IOutboxContext ctx, T message, CancellationToken ctk = default) where T : class
        => PublishAsync(ctx, message, null, ctk)
        ;

    Task PublishAsync<T>(IOutboxContext ctx, T message, IDictionary<string, string>? optionalHeaders = null, CancellationToken ctk = default) where T : class;
}

public interface IOutboxConsumer
{
    Task StartAsync(CancellationToken ctk = default);
    Task StopAsync(CancellationToken ctk = default);

    Task ClearAsync(CancellationToken ctk = default);
    Task<int> CountAsync(CancellationToken ctk = default);
}

public abstract class OutboxConsumerBase : IOutboxConsumer, IDisposable
{
    private readonly Func<IOutboxContext> _outboxContextFactory;
    private CancellationTokenSource? _processorCT;
    private Task? _processorTask;
    private bool _disposedValue;

    public TimeSpan SleepInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int BatchSize { get; set; } = 1000;

    protected OutboxConsumerBase(Func<IOutboxContext> contextFactory)
    {
        _outboxContextFactory = contextFactory;
    }

    public async Task ClearAsync(CancellationToken ctk = default)
    {
        using var ctx = _outboxContextFactory();
        await ctx.ClearAsync(ctk).ConfigureAwait(false);
        ctx.Commit();
    }

    public async Task<int> CountAsync(CancellationToken ctk = default)
    {
        using var ctx = _outboxContextFactory();
        var ret = await ctx.CountAsync(ctk).ConfigureAwait(false);
        ctx.Commit();
        return ret;
    }

    public Task StartAsync(CancellationToken ctk = default)
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
                var messages = await ctx.PeekLockMessagesAsync(BatchSize, ctk).ConfigureAwait(false);
                await _processMessages(messages, ctk).ConfigureAwait(false);
                ctx.Commit();
            }
            catch (Exception e) when (!(e is TaskCanceledException))
            {
                // chomp exceptions and retry
            }
            await Task.Delay(SleepInterval, ctk).ConfigureAwait(false);
        }
    }

    protected virtual async Task _processMessages(IEnumerable<OutboxMessage> messages, CancellationToken ctk)
    {
        foreach (var m in messages)
            await _processMessage(m, ctk).ConfigureAwait(false);
    }

    protected abstract Task _processMessage(OutboxMessage m, CancellationToken ctk);

    public async Task StopAsync(CancellationToken ctk = default)
    {
        _processorCT?.Dispose();
        if (_processorTask is not null)
#pragma warning disable VSTHRD003 // Task.WhenAny is acceptable here for timeout pattern
#pragma warning disable EPC13 // Result should be observed - intentional fire-and-forget with timeout
            await Task.WhenAny(_processorTask, Task.Delay(Timeout.Infinite, ctk)).ConfigureAwait(false);
#pragma warning restore EPC13
#pragma warning restore VSTHRD003
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

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}