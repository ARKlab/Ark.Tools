// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ark.Tools.AspNetCore.HealthChecks.OTel;

/// <summary>
/// Registration extensions for <see cref="ArkOTelHealthCheckPublisher"/>.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Registers <see cref="ArkOTelHealthCheckPublisher"/> as a health check publisher.
    /// Add <see cref="ArkOTelHealthCheckPublisher.MeterName"/> to the OpenTelemetry metrics pipeline to export it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkOTelHealthCheckPublisher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHealthCheckPublisher, ArkOTelHealthCheckPublisher>());
        return services;
    }
}
