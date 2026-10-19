// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.OTel;

/// <summary>
/// Configuration for Ark OpenTelemetry instrumentation.
/// </summary>
public sealed class ArkOtelConfig
{
    /// <summary>Gets the runtime span redaction overrides; fail-closed defaults apply otherwise.</summary>
    public Ark.Tools.Compliance.ComplianceRedactionOptions ComplianceRedaction { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether SQL query text is retained on exported spans.
    /// </summary>
    public bool IncludeSqlQueryText { get; set; }

    /// <summary>
    /// Gets or sets labels for SQL commands that should not produce spans.
    /// The <c>outbox.peek-lock</c> label is skipped by default.
    /// </summary>
    public IEnumerable<string>? SqlQueryLabelsToSkip { get; set; }

    /// <summary>
    /// Gets the names of the metric instruments that are not collected; <c>*</c> and <c>?</c> wildcards are allowed.
    /// By default it holds the HTTP client and .NET runtime metrics that cost storage without helping diagnostics.
    /// Remove a name to collect that metric again, clear the set to collect them all, or add names to drop more.
    /// </summary>
    public ISet<string> DroppedMetrics { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "http.client.open_connections",
        "http.client.active_requests",
        "dotnet.gc.last_collection.heap.fragmentation.size",
        "dotnet.process.cpu.count",
        "dotnet.assembly.count",
        "dotnet.thread_pool.work_item.count",
        "dotnet.jit.*",
    };
}
