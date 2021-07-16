using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TesterWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly SamplingPercentageEstimatorSettings _settings;


        public Worker(ILogger<Worker> logger, TelemetryClient telemetryClient, IOptions<SamplingPercentageEstimatorSettings> settings)
        {
            _logger = logger;
            this._telemetryClient = telemetryClient;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var op = _telemetryClient.StartOperation<RequestTelemetry>("Run"))
                {

                    Console.WriteLine(@$"TW");
                    using var client = new HttpClient();

                    try
                    {
                        using (var d1 = _telemetryClient.StartOperation<DependencyTelemetry>("Dep1"))
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(100));
                        }


                        using (var d1 = _telemetryClient.StartOperation<DependencyTelemetry>("DepFail"))
                        {

                            await Task.Delay(TimeSpan.FromMilliseconds(100));
                            d1.Telemetry.Success = false;
                        }

                        var _ = await client.GetStringAsync("https://www.google.it");

                        using (var d1 = _telemetryClient.StartOperation<DependencyTelemetry>("DepException"))
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(100));
                            throw new Exception();
                        }

                    }
                catch (Exception e)
                    {
                        _telemetryClient.TrackException(e);
                    }
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
