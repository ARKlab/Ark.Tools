// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;
using Ark.Tools.Compliance.OpenApi;

using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.AspNetCore.Swashbuckle;

/// <summary>
/// Registers the OpenAPI schemas of sensitive value objects with Swashbuckle.
/// </summary>
/// <remarks>
/// The schemas themselves are plain <c>Microsoft.OpenApi</c> data owned by
/// <c>Ark.Tools.Compliance.OpenApi</c>; this only binds them to <c>SwaggerGenOptions</c>.
/// They are <c>MapType</c> registrations rather than an <c>ISchemaFilter</c>: a filter
/// reflects over the type at startup, and without a mapping Swashbuckle documents the
/// struct as an object with a <c>value</c> member.
/// </remarks>
public static class SupportComplianceExtensions
{
    /// <summary>
    /// Registers the schema for a sensitive value object.
    /// </summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    /// <param name="c">The Swagger generation options.</param>
    /// <param name="classification">The data classification recorded in the document.</param>
    /// <param name="example">A reserved example value; use <see cref="ComplianceFakes"/>.</param>
    /// <param name="format">The optional OpenAPI string format.</param>
    /// <returns>The same <paramref name="c"/> for chaining.</returns>
    public static SwaggerGenOptions MapSensitiveValue<T>(
        this SwaggerGenOptions c,
        DataClassification classification,
        string example,
        string? format = null)
        where T : struct, ISensitiveValue<T>
    {
        return c.MapSensitiveValue(SensitiveValueSchemaDescriptor.For<T>(classification, example, format));
    }

    /// <summary>
    /// Registers the schemas of the sensitive value objects shipped with
    /// <c>Ark.Tools.Compliance</c>.
    /// </summary>
    /// <param name="c">The Swagger generation options.</param>
    /// <returns>The same <paramref name="c"/> for chaining.</returns>
    public static SwaggerGenOptions MapArkComplianceTypes(this SwaggerGenOptions c)
    {
        foreach (var descriptor in SensitiveValueSchemaDescriptor.ArkTypes)
            c.MapSensitiveValue(descriptor);

        return c;
    }

    /// <summary>
    /// Registers the schema described by <paramref name="descriptor"/>.
    /// </summary>
    /// <param name="c">The Swagger generation options.</param>
    /// <param name="descriptor">The sensitive value object to document.</param>
    /// <returns>The same <paramref name="c"/> for chaining.</returns>
    public static SwaggerGenOptions MapSensitiveValue(this SwaggerGenOptions c, SensitiveValueSchemaDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(descriptor);

        c.MapType(descriptor.Type, () => descriptor.CreateSchema());
        c.MapType(descriptor.NullableType, () => descriptor.CreateSchema(nullable: true));

        return c;
    }
}
