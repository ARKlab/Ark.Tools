// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.LayoutRenderers;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Renders exception details as a fail-closed marker.</summary>
[LayoutRenderer("ark.compliance.exception")]
public sealed class ComplianceExceptionLayoutRenderer : LayoutRenderer
{
    private bool _redact;

    /// <summary>Initializes an exception renderer.</summary>
    public ComplianceExceptionLayoutRenderer()
    {
    }

    /// <inheritdoc />
    protected override void InitializeLayoutRenderer()
    {
        base.InitializeLayoutRenderer();
        _redact = LoggingConfiguration?.Variables.ContainsKey(ComplianceNLogExtensions.RedactionVariable) == true;
    }

    /// <inheritdoc />
    protected override void Append(StringBuilder builder, LogEventInfo logEvent)
    {
        if (logEvent.Exception is null)
            return;

        var redact = _redact
            || LoggingConfiguration?.Variables.ContainsKey(ComplianceNLogExtensions.RedactionVariable) == true
            || LogManager.Configuration?.Variables.ContainsKey(ComplianceNLogExtensions.RedactionVariable) == true;
        builder.Append(redact
            ? ComplianceRedactor.Marker
            : logEvent.Exception.ToString());
    }
}
