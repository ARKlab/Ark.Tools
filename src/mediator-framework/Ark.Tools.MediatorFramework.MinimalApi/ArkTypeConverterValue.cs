// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Wraps a value bound through its registered <see cref="TypeConverter"/>.</summary>
/// <typeparam name="T">The converted value type.</typeparam>
public sealed record ArkTypeConverterValue<T> : IEndpointParameterMetadataProvider
{
    /// <summary>Initializes a new wrapper around the converted value.</summary>
    /// <param name="value">The converted value.</param>
    public ArkTypeConverterValue(T value)
    {
        Value = value;
    }

    /// <summary>Gets the converted value.</summary>
    public T Value { get; }

    /// <inheritdoc />
    public static void PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(builder);

        var name = parameter.GetCustomAttributes()
            .OfType<IFromRouteMetadata>()
            .Select(attribute => attribute.Name)
            .Concat(parameter.GetCustomAttributes().OfType<IFromQueryMetadata>().Select(attribute => attribute.Name))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? parameter.Name
            ?? throw new InvalidOperationException("A type-converter parameter must have a name.");
        builder.Metadata.Add(new ArkTypeConverterParameterMetadata(name, typeof(T)));
    }

    /// <summary>Converts a Minimal API route or query value through the registered type converter.</summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="provider">The format provider supplied by ASP.NET Core.</param>
    /// <param name="result">The wrapped converted value.</param>
    /// <returns><see langword="true"/> when conversion succeeds; otherwise <see langword="false"/>.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Converters are explicitly registered by the host for the closed generated contract types.")]
    [UnconditionalSuppressMessage("Trimming", "IL2087", Justification = "Converters are explicitly registered by the host for the closed generated contract types.")]
    public static bool TryParse(string? value, IFormatProvider? provider, out ArkTypeConverterValue<T> result)
    {
        result = null!;
        if (value is null)
            return false;

        var converter = TypeDescriptor.GetConverter(typeof(T));
        if (!converter.CanConvertFrom(typeof(string)))
            return false;

        try
        {
            var converted = converter.ConvertFrom(null, provider as CultureInfo ?? CultureInfo.InvariantCulture, value);
            if (converted is T typedValue)
            {
                result = new ArkTypeConverterValue<T>(typedValue);
                return true;
            }
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException or ArgumentException)
        {
            return false;
        }

        return false;
    }
}

internal sealed record ArkTypeConverterParameterMetadata(string Name, Type Type);