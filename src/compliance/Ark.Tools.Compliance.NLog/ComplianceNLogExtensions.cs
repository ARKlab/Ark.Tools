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
    /// <returns>The same setup builder for chaining.</returns>
    public static ISetupSerializationBuilder UseComplianceRedaction(
        this ISetupSerializationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ParseMessageTemplates(true);
    }

    /// <summary>Enables NLog message-template parsing.</summary>
    /// <param name="builder">The NLog serialization setup builder.</param>
    /// <returns>The same setup builder for chaining.</returns>
    public static ISetupSerializationBuilder UseArkMessageTemplateParsing(
        this ISetupSerializationBuilder builder)
    {
        return builder.UseComplianceRedaction();
    }

}
