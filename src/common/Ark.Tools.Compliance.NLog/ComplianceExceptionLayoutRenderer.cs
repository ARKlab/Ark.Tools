// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.LayoutRenderers;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Renders exception details as a fail-closed marker.</summary>
[LayoutRenderer("ark.compliance.exception")]
public sealed class ComplianceExceptionLayoutRenderer : ExceptionLayoutRenderer
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
    protected override void AppendMessage(StringBuilder sb, Exception ex)
    {
        if (_isRedactionEnabled())
        {
            sb.Append(ComplianceRedactor.Marker);
            return;
        }

        base.AppendMessage(sb, ex);
    }

    /// <inheritdoc />
    protected override void AppendToString(StringBuilder sb, Exception ex)
    {
        if (_isRedactionEnabled())
        {
            sb.Append(ComplianceRedactor.Marker);
            return;
        }

        base.AppendToString(sb, ex);
    }

    /// <inheritdoc />
    protected override void AppendData(StringBuilder sb, Exception ex)
    {
        if (_isRedactionEnabled() && ex.Data?.Count > 0)
        {
            sb.Append(ComplianceRedactor.Marker);
            return;
        }

        base.AppendData(sb, ex);
    }

    private bool _isRedactionEnabled()
    {
        return _redact
            || LoggingConfiguration?.Variables.ContainsKey(ComplianceNLogExtensions.RedactionVariable) == true
            || LogManager.Configuration?.Variables.ContainsKey(ComplianceNLogExtensions.RedactionVariable) == true;
    }
}
