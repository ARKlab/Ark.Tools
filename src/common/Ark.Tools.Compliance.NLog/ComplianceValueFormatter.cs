// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.MessageTemplates;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Redacts values before NLog formats or destructures message-template arguments.</summary>
public sealed class ComplianceValueFormatter : IValueFormatter
{
    private readonly IValueFormatter _inner;
    private readonly ComplianceRedactor _redactor;

    /// <summary>Initializes a formatter decorator.</summary>
    /// <param name="inner">The original NLog formatter.</param>
    /// <param name="redactor">The immutable runtime policy.</param>
    public ComplianceValueFormatter(IValueFormatter inner, ComplianceRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(redactor);
        _inner = inner;
        _redactor = redactor;
    }

    /// <inheritdoc />
    public bool FormatValue(object? value, string? format, CaptureType captureType, IFormatProvider? formatProvider, StringBuilder builder)
    {
        return _inner.FormatValue(_redactor.Redact(value), format, captureType, CultureInfo.InvariantCulture, builder);
    }
}
