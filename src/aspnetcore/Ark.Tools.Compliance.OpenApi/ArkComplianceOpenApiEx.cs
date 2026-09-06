// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Frozen;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Compliance.Classification;

namespace Ark.Tools.Compliance.OpenApi;

/// <summary>
/// Documents sensitive value objects as their primitive schema, carrying the Ark
/// classification as a vendor extension.
/// </summary>
/// <remarks>
/// The transformers only look up <see cref="Type"/> in a frozen table built at registration
/// time: nothing reflects over the struct, and a classified member documents as its primitive
/// schema instead of an object with a <c>value</c> property.
/// </remarks>
[SuppressMessage("Naming", "CA1711", Justification = "The Ex suffix is part of the public Ark extension API naming convention.")]
public static class ArkComplianceOpenApiEx
{
    /// <summary>Adds the schemas of the sensitive value objects shipped with <c>Ark.Tools.Compliance</c>.</summary>
    /// <param name="options">The OpenAPI options to configure.</param>
    /// <returns>The same options instance.</returns>
    public static OpenApiOptions AddArkComplianceSchemas(this OpenApiOptions options)
    {
        return options.AddSensitiveValueSchemas(SensitiveValueSchemaDescriptor.ArkTypes);
    }

    /// <summary>Adds the schema of a sensitive value object.</summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    /// <param name="options">The OpenAPI options to configure.</param>
    /// <param name="classification">The data classification recorded in the document.</param>
    /// <param name="example">A reserved example value; see <see cref="ComplianceFakes"/>.</param>
    /// <param name="format">The optional OpenAPI string format.</param>
    /// <returns>The same options instance.</returns>
    public static OpenApiOptions AddSensitiveValueSchema<T>(
        this OpenApiOptions options,
        DataClassification classification,
        string example,
        string? format = null)
        where T : struct, ISensitiveValue<T>
    {
        return options.AddSensitiveValueSchemas([SensitiveValueSchemaDescriptor.For<T>(classification, example, format)]);
    }

    /// <summary>Adds the schemas of the given sensitive value objects.</summary>
    /// <param name="options">The OpenAPI options to configure.</param>
    /// <param name="descriptors">The sensitive value objects to document.</param>
    /// <returns>The same options instance.</returns>
    public static OpenApiOptions AddSensitiveValueSchemas(
        this OpenApiOptions options,
        IEnumerable<SensitiveValueSchemaDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(descriptors);

        var map = descriptors.ToFrozenDictionary(static descriptor => descriptor.Type);

        options.AddSchemaTransformer((schema, context, _) =>
        {
            var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
            if (map.TryGetValue(type, out var descriptor))
                descriptor.Apply(schema);

            return Task.CompletedTask;
        });

        return options;
    }
}
