// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.NLog;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Overrides the default Ark NLog runtime redaction policy.</summary>
public static class ComplianceNLogExtensions
{
    /// <summary>Overrides redaction applied by Ark's default target configuration.</summary>
    /// <param name="configurer">The default target configurer.</param>
    /// <param name="configure">Optional overrides of fail-closed defaults.</param>
    /// <returns>The original configurer.</returns>
    public static NLogConfigurer.Configurer WithComplianceRedaction(
        this NLogConfigurer.Configurer configurer,
        Action<ComplianceRedactionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configurer);
        var options = new ComplianceRedactionOptions();
        configure?.Invoke(options);
        ComplianceNLogBootstrap._setPolicy(configurer, new ComplianceRedactor(options));
        return configurer;
    }

    /// <summary>Explicitly disables runtime redaction for this NLog configuration.</summary>
    /// <remarks>Generated sensitive values retain their own safe <c>ToString</c> behavior.</remarks>
    /// <param name="configurer">The default target configurer.</param>
    /// <returns>The original configurer.</returns>
    public static NLogConfigurer.Configurer WithoutComplianceRedaction(this NLogConfigurer.Configurer configurer)
    {
        ArgumentNullException.ThrowIfNull(configurer);
        ComplianceNLogBootstrap._setPolicy(configurer, null);
        return configurer;
    }
}
