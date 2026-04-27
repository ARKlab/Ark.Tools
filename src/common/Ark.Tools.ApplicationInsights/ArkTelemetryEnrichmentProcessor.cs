// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;
using System.Reflection;

namespace Ark.Tools.ApplicationInsights;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that enriches spans with global properties
/// common to all telemetry from this process.
/// </summary>
/// <remarks>
/// Currently adds:
/// <list type="bullet">
/// <item><description><c>ProcessName</c> – the entry assembly name (useful in multi-process environments).</description></item>
/// </list>
/// </remarks>
public sealed class ArkTelemetryEnrichmentProcessor : BaseProcessor<Activity>
{
    private const string _processNameTag = "ProcessName";
    private readonly string? _processName;

    /// <summary>
    /// Initializes a new instance of <see cref="ArkTelemetryEnrichmentProcessor"/>.
    /// </summary>
    public ArkTelemetryEnrichmentProcessor()
    {
        _processName = Assembly.GetEntryAssembly()?.GetName().Name;
    }

    /// <inheritdoc/>
    public override void OnStart(Activity data)
    {
        if (_processName != null && data.GetTagItem(_processNameTag) == null)
            data.SetTag(_processNameTag, _processName);
    }
}
