using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.Extensions.Options;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.ApplicationInsights(net10.0)', Before:
namespace Ark.Tools.ApplicationInsights
{
    public class EnableAdaptiveSamplingWithCustomSettings : IConfigureOptions<TelemetryConfiguration>
    {
        private readonly IOptions<SamplingPercentageEstimatorSettings> _settings;

        public EnableAdaptiveSamplingWithCustomSettings(IOptions<SamplingPercentageEstimatorSettings> settings)
        {
            this._settings = settings;
        }

        public void Configure(TelemetryConfiguration tc)
        {
            void samplingCallback(double ratePerSecond, double currentPercentage, double newPercentage, bool isChanged, SamplingPercentageEstimatorSettings estimatorSettings)
            {
                if (isChanged)
                {
                    tc.SetLastObservedSamplingPercentage(SamplingTelemetryItemTypes.Request, newPercentage);
                }
            }

            tc.DefaultTelemetrySink.TelemetryProcessorChainBuilder
                .UseAdaptiveSampling(_settings.Value, samplingCallback, excludedTypes: "Event")
                .UseAdaptiveSampling(_settings.Value, null, includedTypes: "Event")
                .Build()
                ;
        }
=======
namespace Ark.Tools.ApplicationInsights;

public class EnableAdaptiveSamplingWithCustomSettings : IConfigureOptions<TelemetryConfiguration>
{
    private readonly IOptions<SamplingPercentageEstimatorSettings> _settings;

    public EnableAdaptiveSamplingWithCustomSettings(IOptions<SamplingPercentageEstimatorSettings> settings)
    {
        this._settings = settings;
    }

    public void Configure(TelemetryConfiguration tc)
    {
        void samplingCallback(double ratePerSecond, double currentPercentage, double newPercentage, bool isChanged, SamplingPercentageEstimatorSettings estimatorSettings)
        {
            if (isChanged)
            {
                tc.SetLastObservedSamplingPercentage(SamplingTelemetryItemTypes.Request, newPercentage);
            }
        }

        tc.DefaultTelemetrySink.TelemetryProcessorChainBuilder
            .UseAdaptiveSampling(_settings.Value, samplingCallback, excludedTypes: "Event")
            .UseAdaptiveSampling(_settings.Value, null, includedTypes: "Event")
            .Build()
            ;
>>>>>>> After


namespace Ark.Tools.ApplicationInsights;

public class EnableAdaptiveSamplingWithCustomSettings : IConfigureOptions<TelemetryConfiguration>
{
    private readonly IOptions<SamplingPercentageEstimatorSettings> _settings;

    public EnableAdaptiveSamplingWithCustomSettings(IOptions<SamplingPercentageEstimatorSettings> settings)
    {
        this._settings = settings;
    }

    public void Configure(TelemetryConfiguration tc)
    {
        void samplingCallback(double ratePerSecond, double currentPercentage, double newPercentage, bool isChanged, SamplingPercentageEstimatorSettings estimatorSettings)
        {
            if (isChanged)
            {
                tc.SetLastObservedSamplingPercentage(SamplingTelemetryItemTypes.Request, newPercentage);
            }
        }

        tc.DefaultTelemetrySink.TelemetryProcessorChainBuilder
            .UseAdaptiveSampling(_settings.Value, samplingCallback, excludedTypes: "Event")
            .UseAdaptiveSampling(_settings.Value, null, includedTypes: "Event")
            .Build()
            ;
    }
}