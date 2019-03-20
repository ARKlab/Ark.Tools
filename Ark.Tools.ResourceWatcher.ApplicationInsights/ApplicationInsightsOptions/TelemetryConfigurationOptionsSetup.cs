﻿using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.Extensions.Options;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
using Microsoft.ApplicationInsights.Extensibility.W3C;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    /// <summary>
    /// Initializes TelemetryConfiguration based on values in <see cref="ApplicationInsightsServiceOptions"/>
    /// and registered <see cref="ITelemetryInitializer"/>s and <see cref="ITelemetryModule"/>s.
    /// </summary>
    public class TelemetryConfigurationOptionsSetup : IConfigureOptions<TelemetryConfiguration>
    {
        private readonly ApplicationInsightsServiceOptions applicationInsightsServiceOptions;
        private readonly IEnumerable<ITelemetryInitializer> initializers;
        private readonly IEnumerable<ITelemetryModule> modules;
        private readonly ITelemetryChannel telemetryChannel;
        private readonly IEnumerable<ITelemetryProcessorFactory> telemetryProcessorFactories;
        private readonly IEnumerable<ITelemetryModuleConfigurator> telemetryModuleConfigurators;
        private readonly IApplicationIdProvider applicationIdProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:TelemetryConfigurationOptionsSetup"/> class.
        /// </summary>
        public TelemetryConfigurationOptionsSetup(
            IServiceProvider serviceProvider,
            IOptions<ApplicationInsightsServiceOptions> applicationInsightsServiceOptions,
            IEnumerable<ITelemetryInitializer> initializers,
            IEnumerable<ITelemetryModule> modules,
            IEnumerable<ITelemetryProcessorFactory> telemetryProcessorFactories,
            IEnumerable<ITelemetryModuleConfigurator> telemetryModuleConfigurators)
        {
            this.applicationInsightsServiceOptions = applicationInsightsServiceOptions.Value;
            this.initializers = initializers;
            this.modules = modules;
            this.telemetryProcessorFactories = telemetryProcessorFactories;
            this.telemetryModuleConfigurators = telemetryModuleConfigurators;
            this.telemetryChannel = serviceProvider.GetService<ITelemetryChannel>();
            this.applicationIdProvider = serviceProvider.GetService<IApplicationIdProvider>();
        }

        /// <inheritdoc />
        public void Configure(TelemetryConfiguration configuration)
        {
            if (this.applicationInsightsServiceOptions.InstrumentationKey != null)
            {
                configuration.InstrumentationKey = this.applicationInsightsServiceOptions.InstrumentationKey;
            }

            if (this.telemetryModuleConfigurators.Any())
            {
                foreach (ITelemetryModuleConfigurator telemetryModuleConfigurator in this.telemetryModuleConfigurators)
                {
                    ITelemetryModule telemetryModule = this.modules.FirstOrDefault(((module) => module.GetType() == telemetryModuleConfigurator.TelemetryModuleType));
                    if (telemetryModule != null)
                    {
                        telemetryModuleConfigurator.Configure(telemetryModule, this.applicationInsightsServiceOptions);
                    }
                    else
                    {
                        //TODO MJ
                        //AspNetCoreEventSource.Instance.UnableToFindModuleToConfigure(telemetryModuleConfigurator.TelemetryModuleType.ToString());
                    }
                }
            }

            if (this.telemetryProcessorFactories.Any())
            {
                foreach (ITelemetryProcessorFactory processorFactory in this.telemetryProcessorFactories)
                {
                    configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.Use(processorFactory.Create);
                }
            }

            // Fallback to default channel (InMemoryChannel) created by base sdk if no channel is found in DI
            configuration.TelemetryChannel = this.telemetryChannel ?? configuration.TelemetryChannel;
            (configuration.TelemetryChannel as ITelemetryModule)?.Initialize(configuration);

            this.AddAutoCollectedMetricExtractor(configuration);
            this.AddQuickPulse(configuration);
            this.AddSampling(configuration);
            this.DisableHeartBeatIfConfigured();

            if (applicationInsightsServiceOptions.RequestCollectionOptions.EnableW3CDistributedTracing)
            {
                this.EnableW3CHeaders(configuration);
            }

            configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.Build();
            configuration.TelemetryProcessorChainBuilder.Build();

            if (this.applicationInsightsServiceOptions.DeveloperMode != null)
            {
                configuration.TelemetryChannel.DeveloperMode = this.applicationInsightsServiceOptions.DeveloperMode;
            }

            if (this.applicationInsightsServiceOptions.EndpointAddress != null)
            {
                configuration.TelemetryChannel.EndpointAddress = this.applicationInsightsServiceOptions.EndpointAddress;
            }

            foreach (ITelemetryInitializer initializer in this.initializers)
            {
                configuration.TelemetryInitializers.Add(initializer);
            }

            foreach (ITelemetryModule module in this.modules)
            {
                module.Initialize(configuration);
            }

            foreach (ITelemetryProcessor processor in configuration.TelemetryProcessors)
            {
                ITelemetryModule module = processor as ITelemetryModule;
                if (module != null)
                {
                    module.Initialize(configuration);
                }
            }

            // Microsoft.ApplicationInsights.DependencyCollector.DependencyTrackingTelemetryModule depends on this nullable configuration to support Correlation. 
            configuration.ApplicationIdProvider = this.applicationIdProvider;
        }

        private void AddQuickPulse(TelemetryConfiguration configuration)
        {
            if (this.applicationInsightsServiceOptions.EnableQuickPulseMetricStream)
            {
                QuickPulseTelemetryModule quickPulseModule = this.modules.FirstOrDefault(((module) => module.GetType() == typeof(QuickPulseTelemetryModule))) as QuickPulseTelemetryModule;
                if (quickPulseModule != null)
                {
                    QuickPulseTelemetryProcessor processor = null;
                    configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.Use((next) =>
                    {
                        processor = new QuickPulseTelemetryProcessor(next);
                        quickPulseModule.RegisterTelemetryProcessor(processor);
                        return processor;
                    });
                }
                else
                {
                    //TODO MJ
                    //AspNetCoreEventSource.Instance.UnableToFindQuickPulseModuleInDI();
                }
            }
        }

        private void AddSampling(TelemetryConfiguration configuration)
        {
            if (this.applicationInsightsServiceOptions.EnableAdaptiveSampling)
            {
                configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.UseAdaptiveSampling(5, excludedTypes: "Event;Exception;Request;Trace;Dependency");
                configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.UseAdaptiveSampling(5, includedTypes: "Event");
                configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.UseAdaptiveSampling(5, includedTypes: "Exception");
                configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.UseAdaptiveSampling(5, includedTypes: "Request");
                configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.UseAdaptiveSampling(5, includedTypes: "Trace");
                configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.UseAdaptiveSampling(10, includedTypes: "Dependency");
            }
        }

        private void AddAutoCollectedMetricExtractor(TelemetryConfiguration configuration)
        {
            if (this.applicationInsightsServiceOptions.AddAutoCollectedMetricExtractor)
            {
                configuration.DefaultTelemetrySink.TelemetryProcessorChainBuilder.Use(next => new AutocollectedMetricsExtractor(next));
            }
        }

        private void DisableHeartBeatIfConfigured()
        {
            // Disable heartbeat if user sets it (by default it is on)
            if (!this.applicationInsightsServiceOptions.EnableHeartbeat)
            {
                foreach (var module in TelemetryModules.Instance.Modules)
                {
                    if (module is IHeartbeatPropertyManager hbeatMan)
                    {
                        hbeatMan.IsHeartbeatEnabled = false;
                    }
                }
            }
        }

        private void EnableW3CHeaders(TelemetryConfiguration configuration)
        {
            configuration.TelemetryInitializers.Add(new W3COperationCorrelationTelemetryInitializer());
        }
    }
}