// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Resources;

using System.Reflection;

namespace Ark.Tools.OTel;

/// <summary>
/// Provides resource configuration for Ark OpenTelemetry integrations.
/// </summary>
public static class ArkTelemetryResourceExtensions
{
    /// <summary>
    /// Adds the entry assembly name and version as the OpenTelemetry service name and version. The version is the
    /// informational version, commit included (for example <c>1.2.3+abc123</c>), or else the assembly version.
    /// <c>OTEL_SERVICE_NAME</c> and <c>OTEL_RESOURCE_ATTRIBUTES</c> override them.
    /// </summary>
    /// <param name="builder">The OpenTelemetry resource builder.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static ResourceBuilder AddArkTelemetryResource(this ResourceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var entryAssembly = Assembly.GetEntryAssembly();
        var serviceName = entryAssembly?.GetName().Name;
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            var serviceVersion = entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? entryAssembly?.GetName().Version?.ToString();
            builder.AddService(serviceName, serviceVersion: serviceVersion);
        }

        // Later attributes win: read the environment again so it overrides the defaults above.
        return builder.AddEnvironmentVariableDetector();
    }
}
