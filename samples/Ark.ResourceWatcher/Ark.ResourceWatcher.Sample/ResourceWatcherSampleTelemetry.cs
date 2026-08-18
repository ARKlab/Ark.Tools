// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.ResourceWatcher.Sample;

internal static class ResourceWatcherSampleTelemetry
{
    internal const string ActivitySourceName = "ark.resourcewatcher.sample";
    internal const string MeterName = ActivitySourceName;

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    internal static readonly Meter Meter = new(MeterName);
    internal static readonly Counter<long> ProcessedRecords =
        Meter.CreateCounter<long>("ark.resourcewatcher.sample.records_processed");
    internal static readonly Histogram<double> ProcessingDuration =
        Meter.CreateHistogram<double>("ark.resourcewatcher.sample.processing_duration", unit: "ms");
}
