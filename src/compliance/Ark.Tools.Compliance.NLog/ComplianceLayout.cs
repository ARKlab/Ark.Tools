// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.Layouts;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Applies compliance pattern scanning to a configured layout.</summary>
public sealed class ComplianceLayout : Layout
{
    private readonly Layout _inner;
    private readonly Func<PiiScanner?> _scannerProvider;

    /// <summary>Initializes a layout wrapper.</summary>
    /// <param name="inner">The configured layout to render.</param>
    /// <param name="scannerProvider">Provides the current PII scanner.</param>
    public ComplianceLayout(Layout inner, Func<PiiScanner?> scannerProvider)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(scannerProvider);
        _inner = inner;
        _scannerProvider = scannerProvider;
    }

    protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
    {
        var rendered = _inner.Render(logEvent);
        target.Append(_scannerProvider()?.Scan(rendered) ?? rendered);
    }

    protected override string GetFormattedMessage(LogEventInfo logEvent)
    {
        var rendered = _inner.Render(logEvent);
        return _scannerProvider()?.Scan(rendered) ?? rendered;
    }
}
