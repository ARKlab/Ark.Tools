// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.Config;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Registers the stateless NLog adapters used by Ark compliance configuration.</summary>
public static class ComplianceNLogExtensions
{
    /// <summary>The configuration variable that enables exception redaction.</summary>
    public const string RedactionVariable = "ArkComplianceRedaction";

    /// <summary>Enables NLog message-template parsing and compliance-aware value formatting.</summary>
    /// <param name="builder">The NLog serialization setup builder.</param>
    /// <param name="formatter">The formatter used for values after redaction.</param>
    /// <param name="redactor">The immutable runtime redaction policy.</param>
    /// <returns>The same setup builder for chaining.</returns>
    public static ISetupSerializationBuilder UseComplianceRedaction(
        this ISetupSerializationBuilder builder,
        IValueFormatter formatter,
        ComplianceRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(redactor);

        return builder
            .ParseMessageTemplates(true)
            .RegisterValueFormatter(new ComplianceValueFormatter(formatter, redactor))
            .RegisterObjectTransformation<IRuntimeClassifiedValue>(value => redactor.Redact(value)!);
    }

    /// <summary>Enables NLog message-template parsing with an ordinary value formatter.</summary>
    /// <param name="builder">The NLog serialization setup builder.</param>
    /// <param name="formatter">The formatter to register.</param>
    /// <returns>The same setup builder for chaining.</returns>
    public static ISetupSerializationBuilder UseArkMessageTemplateParsing(
        this ISetupSerializationBuilder builder,
        IValueFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(formatter);

        return builder
            .ParseMessageTemplates(true)
            .RegisterValueFormatter(formatter);
    }

    /// <summary>Registers the layout renderer used for exception redaction.</summary>
    /// <param name="builder">The NLog extensions setup builder.</param>
    /// <returns>The same setup builder for chaining.</returns>
    public static ISetupExtensionsBuilder RegisterComplianceLayoutRenderers(
        this ISetupExtensionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .RegisterLayoutRenderer<ComplianceExceptionLayoutRenderer>("ark.compliance.exception")
            ;
    }
}
