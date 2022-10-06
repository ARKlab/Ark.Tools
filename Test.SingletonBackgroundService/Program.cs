using Ark.Tools.Hosting;

using Medallion.Threading;
using Medallion.Threading.Azure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Test.SingletonBackgroundService
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((ctx,s) =>
                {
                    s.AddSingleton<IDistributedLockProvider>(
                        new AzureBlobLeaseDistributedSynchronizationProvider(
                            new Azure.Storage.Blobs.BlobContainerClient(ctx.Configuration["ConnectionStrings:Storage"],"locks")));
                    s.AddHostedService<RunEvery30Sec>();
                    s.AddHostedService<RunForeverThrowsRandomly>();
                })
                .Build();

            await host.RunAsync();
        }
    }

    internal class RunEvery30Sec : Ark.Tools.Hosting.SingletonBackgroundService
    {
        public RunEvery30Sec(IDistributedLockProvider distributedLockProvider, ILogger<RunEvery30Sec> logger, string? serviceName = null) 
            : base(distributedLockProvider, logger, serviceName)
        {
            Cooldown = TimeSpan.FromSeconds(30);
        }

        protected override async Task RunAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(nameof(RunEvery30Sec));
            await Task.Delay(2000, stoppingToken);
        }
    }

    internal class RunForeverThrowsRandomly : Ark.Tools.Hosting.SingletonBackgroundService
    {
        private readonly Random _random;

        public RunForeverThrowsRandomly(IDistributedLockProvider distributedLockProvider, ILogger<RunForeverThrowsRandomly> logger, string? serviceName = null)
            : base(distributedLockProvider, logger, serviceName)
        {
            _random = new Random();
        }

        protected override async Task RunAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(nameof(RunForeverThrowsRandomly));

                if (_random.NextDouble() > 0.8) throw new ApplicationException("Random crash");

                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}