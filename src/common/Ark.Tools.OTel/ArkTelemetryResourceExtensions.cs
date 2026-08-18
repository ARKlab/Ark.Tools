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
    /// Adds the entry assembly name as the OpenTelemetry service name.
    /// </summary>
    /// <param name="builder">The OpenTelemetry resource builder.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static ResourceBuilder AddArkTelemetryResource(this ResourceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var serviceName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrWhiteSpace(serviceName))
            builder.AddService(serviceName);

        return builder;
    }
}
