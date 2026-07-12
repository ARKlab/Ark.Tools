// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.OpenApi;

using Microsoft.OpenApi;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>OpenAPI conventions used by Ark Minimal API hosts.</summary>
[SuppressMessage("Naming", "CA1711", Justification = "The Ex suffix is part of the public Ark extension API naming convention.")]
public static class ArkOpenApiEx
{
    /// <summary>Adds Ark schema formats for supported NodaTime types.</summary>
    /// <param name="options">The OpenAPI options to configure.</param>
    /// <returns>The same options instance.</returns>
    public static OpenApiOptions AddArkNodaTimeSchemas(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddSchemaTransformer((schema, context, _) =>
        {
            var format = context.JsonTypeInfo.Type switch
            {
                var type when type == typeof(NodaTime.LocalDate) => "date",
                var type when type == typeof(NodaTime.LocalDateTime) => "local-date-time",
                var type when type == typeof(NodaTime.OffsetDateTime) => "date-time",
                var type when type == typeof(NodaTime.Period) => "nodatime-period",
                _ => null,
            };

            if (format is not null)
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = format;
            }

            return Task.CompletedTask;
        });

        return options;
    }

    /// <summary>
    /// Adds an OpenAPI <c>oneOf</c> schema and discriminator for a polymorphic hierarchy.
    /// </summary>
    /// <typeparam name="TBase">The polymorphic base type.</typeparam>
    /// <typeparam name="TDiscriminator">The discriminator value type.</typeparam>
    /// <param name="options">The OpenAPI options to configure.</param>
    /// <param name="discriminatorProperty">The discriminator property name.</param>
    /// <param name="mapping">The discriminator values and derived types.</param>
    /// <returns>The same options instance.</returns>
    public static OpenApiOptions AddArkPolymorphism<TBase, TDiscriminator>(
        this OpenApiOptions options,
        string discriminatorProperty,
        params (TDiscriminator Value, Type DerivedType)[] mapping)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminatorProperty);
        ArgumentNullException.ThrowIfNull(mapping);

        options.AddSchemaTransformer(async (schema, context, cancellationToken) =>
        {
            if (context.JsonTypeInfo.Type != typeof(TBase))
                return;

            var document = context.Document
                ?? throw new InvalidOperationException("OpenAPI schema transformer requires a document.");
            var references = new List<(string Value, OpenApiSchemaReference Reference)>();
            foreach (var (value, derivedType) in mapping)
            {
                ArgumentNullException.ThrowIfNull(derivedType);

                var componentName = derivedType.Name;
                var derivedSchema = await context.GetOrCreateSchemaAsync(
                    derivedType,
                    null,
                    cancellationToken).ConfigureAwait(false);
                document.AddComponent(componentName, derivedSchema);
                references.Add((
                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                        ?? throw new InvalidOperationException("A discriminator value must have a string representation."),
                    new OpenApiSchemaReference(componentName, document)));
            }

            schema.OneOf = new List<IOpenApiSchema>(references.Select(item => (IOpenApiSchema)item.Reference));
            schema.Discriminator = new OpenApiDiscriminator
            {
                PropertyName = discriminatorProperty,
                Mapping = references.ToDictionary(
                    item => item.Value,
                    item => item.Reference,
                    StringComparer.Ordinal),
            };
        });

        return options;
    }
}
