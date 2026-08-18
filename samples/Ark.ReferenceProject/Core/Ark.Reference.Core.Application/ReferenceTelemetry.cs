// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Core.Common.Dto;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.Reference.Core.Application;

/// <summary>
/// Defines the custom OpenTelemetry signals emitted by the reference application.
/// </summary>
public static class ReferenceTelemetry
{
    /// <summary>
    /// The activity source name for application-level operations.
    /// </summary>
    public const string ActivitySourceName = "ark.reference.core.application";

    /// <summary>
    /// The meter name for application-level measurements.
    /// </summary>
    public const string MeterName = ActivitySourceName;

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    internal static readonly Meter Meter = new(MeterName);
    internal static readonly Counter<long> CompletedProcesses =
        Meter.CreateCounter<long>("ark.reference.book_print_process.completed");
    internal static readonly Histogram<double> Progress =
        Meter.CreateHistogram<double>("ark.reference.book_print_process.progress", unit: "ratio");

    /// <summary>
    /// Records the final state of a book print process.
    /// </summary>
    /// <param name="process">The process state to record.</param>
    public static void RecordProcess(BookPrintProcess.V1.Output process)
    {
        ArgumentNullException.ThrowIfNull(process);

        var status = process.Status.ToString();
        Progress.Record(process.Progress, new KeyValuePair<string, object?>("process.status", status));
        if (status.Equals("Completed", StringComparison.Ordinal))
            CompletedProcesses.Add(1);
    }
}
