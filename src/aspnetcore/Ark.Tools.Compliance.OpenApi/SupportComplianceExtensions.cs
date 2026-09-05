// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json.Nodes;

using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.Compliance.OpenApi;

/// <summary>
/// Documents sensitive value objects as their primitive schema, carrying the Ark
/// classification as a vendor extension.
/// </summary>
/// <remarks>
/// The mappings are <c>MapType</c> registrations rather than an <c>ISchemaFilter</c>: a
/// filter reflects over the type at startup, which is AoT-hostile, and without a mapping
/// Swashbuckle documents the struct as an object with a <c>value</c> member.
/// </remarks>
public static class SupportComplianceExtensions
{
    /// <summary>The vendor extension carrying the Ark data classification.</summary>
    public const string ClassificationExtension = "x-ark-classification";

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
        ArgumentNullException.ThrowIfNull(c);
        ArgumentException.ThrowIfNullOrWhiteSpace(example);

        c.MapType<T>(() => _schema(classification, example, format, JsonSchemaType.String));
        c.MapType<T?>(() => _schema(classification, example, format, JsonSchemaType.String | JsonSchemaType.Null));

        return c;
    }

    /// <summary>
    /// Registers the schemas of the sensitive value objects shipped with
    /// <c>Ark.Tools.Compliance</c>.
    /// </summary>
    /// <param name="c">The Swagger generation options.</param>
    /// <returns>The same <paramref name="c"/> for chaining.</returns>
    public static SwaggerGenOptions MapArkComplianceTypes(this SwaggerGenOptions c)
    {
        c.MapSensitiveValue<EmailAddress>(ArkDataClassifications.PersonalData, ComplianceFakes.Email(), "email");
        c.MapSensitiveValue<PhoneNumber>(ArkDataClassifications.PersonalData, ComplianceFakes.PhoneNumber());
        c.MapSensitiveValue<PersonName>(ArkDataClassifications.PersonalData, ComplianceFakes.PersonName());
        c.MapSensitiveValue<PostalAddressLine>(ArkDataClassifications.PersonalData, ComplianceFakes.PostalAddressLine());
        c.MapSensitiveValue<NationalIdentifier>(ArkDataClassifications.SensitivePersonalData, ComplianceFakes.NationalIdentifier());
        c.MapSensitiveValue<ApiKey>(ArkDataClassifications.Secret, ComplianceFakes.ApiKey(), "password");

        return c;
    }

    private static OpenApiSchema _schema(DataClassification classification, string example, string? format, JsonSchemaType type)
    {
        var schema = new OpenApiSchema
        {
            Type = type,
            Format = format,
            Examples = [JsonValue.Create(example)],
        };

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        schema.Extensions[ClassificationExtension] = new JsonNodeExtension(
            $"{classification.TaxonomyName}:{classification.Value}");

        return schema;
    }
}
