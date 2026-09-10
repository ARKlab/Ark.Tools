// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.Config;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Registers the stateless NLog adapters used by Ark compliance configuration.</summary>
public static class ComplianceNLogExtensions
{
    /// <summary>Enables NLog message-template parsing for safe generated value formatting.</summary>
    /// <param name="builder">The NLog serialization setup builder.</param>
    /// <param name="formatter">The formatter used for ordinary values.</param>
    /// <returns>The same setup builder for chaining.</returns>
    public static ISetupSerializationBuilder UseComplianceRedaction(
        this ISetupSerializationBuilder builder,
        IValueFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(formatter);

        return builder
            .ParseMessageTemplates(true)
            .RegisterValueFormatter(formatter);
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

}
