using NLog;

using System.Security.Claims;

namespace LinuxWebJobHosting.Utils
{
    public class HostedService : BackgroundService
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        
        public HostedService()
        {
            var enabled = _logger.IsEnabled(NLog.LogLevel.Info);
            Console.WriteLine($"HostedService constructor{enabled}");
        }

        protected override async Task ExecuteAsync(CancellationToken ctk)
        {
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
            Console.WriteLine("WebJob: I am alive");
            _logger.Info("I am alive");
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }

    }
}
