using Microsoft.Extensions.Hosting;

using NLog;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace LinuxWebJobHosting.Utils
{
    public class HostedService : BackgroundService
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly Random _random;

        public HostedService()
        {
            var enabled = _logger.IsEnabled(NLog.LogLevel.Info);
            _random = new Random();
        }

        protected override async Task ExecuteAsync(CancellationToken ctk)
        {
            using var hostName = ScopeContext.PushProperty("AppName", nameof(HostedService));
            _logger.Info("WebJob: Start");
            while (!ctk.IsCancellationRequested)
            {
                try
                {
                    await _do(ctk);
                }
                catch (OperationCanceledException) when (ctk.IsCancellationRequested) { }
                catch (Exception e)
                {
                    _logger.Error(e, "Run failed");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ctk);
                }
                catch (OperationCanceledException) when (ctk.IsCancellationRequested) { }
            }

        }

        private async Task _do(CancellationToken cancellationToken)
        {
            _logger.Info("I am alive");
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            if (_random.NextDouble() > 0.8)
                throw new InvalidOperationException("Random crash");
        }

    }
}
