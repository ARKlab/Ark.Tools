using NLog;

using Rebus.Handlers;

using System.Diagnostics;

namespace Ark.Tools.Rebus;

public class RebusLogDecorator<T> : IHandleMessages<T>
{
    private readonly IHandleMessages<T> _inner;
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public RebusLogDecorator(IHandleMessages<T> inner)
    {
        _inner = inner;
    }

    public async Task Handle(T message)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _inner.Handle(message).ConfigureAwait(false);
            _logger.Debug("Processed message type {Type} in {Elapsed}ms", typeof(T).FullName, sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            _logger.Warn(e, CultureInfo.InvariantCulture, "Failed processing message type {Type} in {Elapsed}ms", typeof(T).FullName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}