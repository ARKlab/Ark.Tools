// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>Scans untyped span text before it reaches an exporter.</summary>
/// <remarks>
/// Register before exporters. Generated sensitive values own their safe formatting;
/// this processor only scans strings when PII scanning is enabled.
/// Event, link and baggage attributes are outside this processor's scope.
/// </remarks>
public sealed class ArkComplianceRedactionProcessor : BaseProcessor<Activity>
{
    private readonly PiiScanner _scanner;

    /// <summary>Initializes a span processor with PII scanning disabled by default.</summary>
    /// <param name="options">Optional policy overrides.</param>
    public ArkComplianceRedactionProcessor(ComplianceRedactionOptions? options = null)
    {
        _scanner = new(options is null ? PiiScanMode.Off : options.PiiScan);
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var tag in data.TagObjects.ToArray())
        {
            if (tag.Value is string text)
                data.SetTag(tag.Key, _scanner.Scan(text));
        }
        if (_scanner.IsEnabled)
        {
            data.DisplayName = _scanner.Scan(data.DisplayName);
            if (data.StatusDescription is not null)
                data.SetStatus(data.Status, _scanner.Scan(data.StatusDescription));
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
