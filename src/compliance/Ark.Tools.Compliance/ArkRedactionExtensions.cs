// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Telemetry;

namespace Ark.Tools.Compliance;

/// <summary>
/// Registers Ark's fail-closed redaction policy for Microsoft logging.
/// </summary>
public static class ArkRedactionExtensions
{
    /// <summary>
    /// Adds Ark redactors and enables redaction in the Microsoft logging pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddArkRedaction(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRedaction(static builder =>
        {
            builder.SetRedactor<ArkMaskingRedactor>(ArkDataClassifications.PersonalData);
            builder.SetRedactor<ArkErasingRedactor>(
                ArkDataClassifications.SensitivePersonalData,
                ArkDataClassifications.Secret,
                ArkDataClassifications.Pseudonymous);
            builder.SetFallbackRedactor<ArkErasingRedactor>();
        });
        services.AddLogging(static logging => logging.EnableRedaction());
        return services;
    }
}
