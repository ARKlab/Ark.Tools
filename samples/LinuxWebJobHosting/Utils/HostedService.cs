using Microsoft.Extensions.Hosting;

using NLog;

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace LinuxWebJobHosting.Utils;

public class HostedService : BackgroundService
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Random _random;

    public HostedService()
    {
        var enabled = _logger.IsEnabled(NLog.LogLevel.Info);
        _random = new Random();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var hostName = ScopeContext.PushProperty("AppName", nameof(HostedService));
        _logger.Info(CultureInfo.InvariantCulture, "WebJob: Start");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _do(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e)
            {
                _logger.Error(e, "Run failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }

    }

    private async Task _do(CancellationToken cancellationToken)
    {
        _logger.Info(CultureInfo.InvariantCulture, "I am alive");
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
#pragma warning disable CA5394 // Do not use insecure randomness
        if (_random.NextDouble() > 0.8)
            throw new InvalidOperationException("Random crash");
#pragma warning restore CA5394 // Do not use insecure randomness
    }

}