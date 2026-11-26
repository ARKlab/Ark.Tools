// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json.Nodes;

namespace Ark.Tools.Compliance.OpenApi;

/// <summary>
/// Builds the <c>Microsoft.OpenApi</c> schema of a <see cref="SensitiveValueSchemaDescriptor"/>.
/// </summary>
/// <remarks>
/// Shared source, compiled into each OpenAPI integration against the <c>Microsoft.OpenApi</c>
/// version that integration ships with.
/// </remarks>
[SuppressMessage("Naming", "CA1711", Justification = "The Ex suffix is part of the public Ark extension API naming convention.")]
static partial class SensitiveValueSchemaDescriptorEx
{
    /// <summary>
    /// Rewrites a schema as the primitive form of the sensitive value object, preserving any
    /// nullability already established by the document generator.
    /// </summary>
    /// <param name="descriptor">The sensitive value object to document.</param>
    /// <param name="schema">The schema to rewrite.</param>
    public static void Apply(this SensitiveValueSchemaDescriptor descriptor, OpenApiSchema schema)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(schema);

        schema.Type = JsonSchemaType.String | (schema.Type.GetValueOrDefault() & JsonSchemaType.Null);
        schema.Format = descriptor.Format;
        schema.Examples = [JsonValue.Create(descriptor.Example)];
        schema.Properties = null;

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        schema.Extensions[SensitiveValueSchemaDescriptor.ClassificationExtension] = new JsonNodeExtension(descriptor.ClassificationValue);
    }

    /// <summary>
    /// Creates the schema of the sensitive value object.
    /// </summary>
    /// <param name="descriptor">The sensitive value object to document.</param>
    /// <param name="nullable">Whether the schema also admits <c>null</c>.</param>
    /// <returns>The schema.</returns>
    public static OpenApiSchema CreateSchema(this SensitiveValueSchemaDescriptor descriptor, bool nullable = false)
    {
        var schema = new OpenApiSchema { Type = nullable ? JsonSchemaType.Null : null };
        descriptor.Apply(schema);
        return schema;
    }
}
