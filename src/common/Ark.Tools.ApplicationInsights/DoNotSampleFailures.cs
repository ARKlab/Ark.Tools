// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.ApplicationInsights(net10.0)', Before:
namespace Ark.Tools.AspNetCore.ApplicationInsights
{
    public class DoNotSampleFailures
        : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is ExceptionTelemetry || (telemetry is DependencyTelemetry dp && dp.Success == false) || (telemetry is RequestTelemetry rq && rq.Success == false))
            {
                if (telemetry is ISupportSampling s)
                    s.SamplingPercentage = 100;
            }
=======
namespace Ark.Tools.AspNetCore.ApplicationInsights;

public class DoNotSampleFailures
    : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is ExceptionTelemetry || (telemetry is DependencyTelemetry dp && dp.Success == false) || (telemetry is RequestTelemetry rq && rq.Success == false))
        {
            if (telemetry is ISupportSampling s)
                s.SamplingPercentage = 100;
>>>>>>> After


namespace Ark.Tools.AspNetCore.ApplicationInsights;

    public class DoNotSampleFailures
        : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is ExceptionTelemetry || (telemetry is DependencyTelemetry dp && dp.Success == false) || (telemetry is RequestTelemetry rq && rq.Success == false))
            {
                if (telemetry is ISupportSampling s)
                    s.SamplingPercentage = 100;
            }
        }
    }