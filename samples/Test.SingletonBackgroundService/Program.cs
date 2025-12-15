using Medallion.Threading;
using Medallion.Threading.Azure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Test.SingletonBackgroundService
{
    internal sealed class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((ctx, s) =>
                {
                    s.AddSingleton<IDistributedLockProvider>(
                        new AzureBlobLeaseDistributedSynchronizationProvider(
                            new Azure.Storage.Blobs.BlobContainerClient(ctx.Configuration["ConnectionStrings:Storage"], "locks")));
                    s.AddHostedService<RunEvery30Sec>();
                    s.AddHostedService<RunForeverThrowsRandomly>();
                })
                .Build();

            await host.RunAsync();
        }
    }

    internal sealed partial class RunEvery30Sec : Ark.Tools.Hosting.SingletonBackgroundService
    {
        public RunEvery30Sec(IDistributedLockProvider distributedLockProvider, ILogger<RunEvery30Sec> logger, string? serviceName = null)
            : base(distributedLockProvider, logger, serviceName)
        {
            Cooldown = TimeSpan.FromSeconds(30);
        }

        protected override async Task RunAsync(CancellationToken stoppingToken)
        {
            LogRunEvery30Sec(_logger);
            await Task.Delay(2000, stoppingToken);
        }

        [LoggerMessage(Level = LogLevel.Information, Message = nameof(RunEvery30Sec))]
        private static partial void LogRunEvery30Sec(ILogger logger);
    }

    internal sealed partial class RunForeverThrowsRandomly : Ark.Tools.Hosting.SingletonBackgroundService
    {
        private readonly Random _random;

        public RunForeverThrowsRandomly(IDistributedLockProvider distributedLockProvider, ILogger<RunForeverThrowsRandomly> logger, string? serviceName = null)
            : base(distributedLockProvider, logger, serviceName)
        {
            _random = new Random();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Test code")]
        protected override async Task RunAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                LogRunForeverThrowsRandomly(_logger);

                if (_random.NextDouble() > 0.8) throw new InvalidOperationException("Random crash");

                await Task.Delay(3000, stoppingToken);
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = nameof(RunForeverThrowsRandomly))]
        private static partial void LogRunForeverThrowsRandomly(ILogger logger);
    }
}