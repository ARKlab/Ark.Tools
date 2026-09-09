// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.Layouts;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Applies compliance pattern scanning to a configured layout.</summary>
public sealed class ComplianceLayout : Layout
{
    private readonly Layout _inner;
    private readonly Func<ComplianceRedactor?> _redactorProvider;

    /// <summary>Initializes a layout wrapper.</summary>
    /// <param name="inner">The configured layout to render.</param>
    /// <param name="redactorProvider">Provides the current runtime redaction policy.</param>
    public ComplianceLayout(Layout inner, Func<ComplianceRedactor?> redactorProvider)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(redactorProvider);
        _inner = inner;
        _redactorProvider = redactorProvider;
    }

    protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
    {
        var rendered = _inner.Render(logEvent);
        target.Append(_redactorProvider()?.Scan(rendered) ?? rendered);
    }

    protected override string GetFormattedMessage(LogEventInfo logEvent)
    {
        var rendered = _inner.Render(logEvent);
        return _redactorProvider()?.Scan(rendered) ?? rendered;
    }
}
