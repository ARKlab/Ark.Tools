// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>Redacts classified span attributes before they reach an exporter.</summary>
/// <remarks>
/// Register before exporters. String tags have no CLR member classification metadata;
/// retain sensitive value objects until this processor, or enable pattern scanning.
/// Event, link and baggage attributes are outside this processor's scope.
/// </remarks>
public sealed class ArkComplianceRedactionProcessor : BaseProcessor<Activity>
{
    private readonly ComplianceRedactor _redactor;

    /// <summary>Initializes a span processor with fail-closed defaults.</summary>
    /// <param name="options">Optional policy overrides.</param>
    public ArkComplianceRedactionProcessor(ComplianceRedactionOptions? options = null)
    {
        _redactor = new(options);
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var tag in data.TagObjects.ToArray())
            data.SetTag(tag.Key, _redactor.Redact(tag.Value));
        if (_redactor.PatternScan != PatternScanMode.Off)
        {
            data.DisplayName = _redactor.Scan(data.DisplayName);
            if (data.StatusDescription is not null)
                data.SetStatus(data.Status, _redactor.Scan(data.StatusDescription));
        }
    }
}

/// <summary>Registers Ark's runtime redaction before tracing exporters.</summary>
public static class ArkComplianceRedactionExtensions
{
    /// <summary>Adds classification-aware redaction to a tracing pipeline.</summary>
    /// <param name="builder">The tracing builder.</param>
    /// <param name="options">Optional policy overrides.</param>
    /// <returns>The tracing builder.</returns>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The tracing provider owns and disposes registered processors.")]
    public static TracerProviderBuilder AddArkComplianceRedaction(this TracerProviderBuilder builder, ComplianceRedactionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddProcessor(new ArkComplianceRedactionProcessor(options));
    }
}
