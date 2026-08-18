// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Pipeline.Send;
using Rebus.Time;

using SimpleInjector;

namespace Ark.Tools.Rebus;

/// <summary>
/// Registers the opt-in Application Insights v3 Rebus adapters.
/// </summary>
public static class ApplicationInsightsExtensions
{
    /// <summary>
    /// Adds Application Insights request telemetry for Rebus messages.
    /// </summary>
    /// <param name="configurer">The Rebus options configurator.</param>
    /// <param name="container">The application SimpleInjector container.</param>
    public static void UseApplicationInsight(this OptionsConfigurer configurer, Container container)
    {
        ArgumentNullException.ThrowIfNull(configurer);
        ArgumentNullException.ThrowIfNull(container);
        configurer.Decorate<IPipeline>(c =>
        {
            var pipeline = c.Get<IPipeline>();
            var step = new ApplicationInsightsStep(container);
            return new PipelineStepConcatenator(
                new PipelineStepInjector(pipeline)
                    .OnSend(step, PipelineRelativePosition.Before, typeof(SerializeOutgoingMessageStep)))
                .OnReceive(step, PipelineAbsolutePosition.Front);
        });
    }

    /// <summary>
    /// Adds Application Insights custom metrics for Rebus processing.
    /// </summary>
    /// <param name="configurer">The Rebus options configurator.</param>
    /// <param name="container">The application SimpleInjector container.</param>
    public static void UseApplicationInsightMetrics(this OptionsConfigurer configurer, Container container)
    {
        ArgumentNullException.ThrowIfNull(configurer);
        ArgumentNullException.ThrowIfNull(container);
        configurer.Decorate<IPipeline>(c =>
        {
            var pipeline = c.Get<IPipeline>();
            var time = c.Get<IRebusTime>();
            var step = new ApplicationInsightsProcessingMetricsStep(container, time);
            return new PipelineStepInjector(pipeline)
                .OnReceive(step, PipelineRelativePosition.Before, typeof(DispatchIncomingMessageStep));
        });
    }
}
