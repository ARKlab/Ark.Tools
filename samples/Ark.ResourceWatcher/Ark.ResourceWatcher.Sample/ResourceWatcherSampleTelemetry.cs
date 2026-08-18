// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.ResourceWatcher.Sample;

internal static class ResourceWatcherSampleTelemetry
{
    internal const string _activitySourceName = "ark.resourcewatcher.sample";
    internal const string _meterName = _activitySourceName;

    internal static readonly ActivitySource _activitySource = new(_activitySourceName);
    internal static readonly Meter _meter = new(_meterName);
    internal static readonly Counter<long> _processedRecords =
        _meter.CreateCounter<long>("ark.resourcewatcher.sample.records_processed");
    internal static readonly Histogram<double> _processingDuration =
        _meter.CreateHistogram<double>("ark.resourcewatcher.sample.processing_duration", unit: "ms");
}
