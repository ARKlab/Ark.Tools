// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.API;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>
/// Defines the custom OpenTelemetry signals emitted by the mediator sample.
/// </summary>
public static class SampleTelemetry
{
    /// <summary>
    /// The activity source name for sample application operations.
    /// </summary>
    public const string ActivitySourceName = "ark.mediator.sample.application";

    /// <summary>
    /// The meter name for sample application measurements.
    /// </summary>
    public const string MeterName = ActivitySourceName;

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    internal static readonly Meter Meter = new(MeterName);
    internal static readonly Counter<long> CompletedProcesses =
        Meter.CreateCounter<long>("ark.mediator.sample.book_print_process.completed");
    internal static readonly Histogram<double> Progress =
        Meter.CreateHistogram<double>("ark.mediator.sample.book_print_process.progress", unit: "ratio");

    /// <summary>
    /// Records the final state of a book print process.
    /// </summary>
    /// <param name="process">The process state to record.</param>
    public static void RecordProcess(BookPrintProcessResponse process)
    {
        ArgumentNullException.ThrowIfNull(process);

        var status = process.Status.ToString();
        Progress.Record(process.Progress, new KeyValuePair<string, object?>("process.status", status));
        if (status.Equals("Completed", StringComparison.Ordinal))
            CompletedProcesses.Add(1);
    }
}
