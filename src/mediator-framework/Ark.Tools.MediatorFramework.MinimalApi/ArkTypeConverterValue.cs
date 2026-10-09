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
            .Select(static attribute => attribute.Name)
            .Concat(parameter.GetCustomAttributes().OfType<IFromQueryMetadata>().Select(static attribute => attribute.Name))
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            ?? parameter.Name
            ?? throw new InvalidOperationException("A type-converter parameter must have a name.");
        builder.Metadata.Add(new ArkTypeConverterParameterMetadata(name, typeof(T)));
    }

    /// <summary>Converts a Minimal API route or query value through the registered type converter.</summary>
    /// <remarks>
    /// The converter is resolved trim-safely with <see cref="TypeDescriptor.GetConverterFromRegisteredType(Type)"/>, so
    /// the type must be a primitive, an enum, or registered with <see cref="TypeDescriptor.RegisterType{T}"/> before
    /// it is first looked up or its converter is added; for NodaTime types call <c>Ark.Tools.Nodatime.NodaTimeConverter.Register()</c>. An
    /// unregistered type throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    /// <param name="value">The value to convert.</param>
    /// <param name="provider">The format provider supplied by ASP.NET Core.</param>
    /// <param name="result">The wrapped converted value.</param>
    /// <returns><see langword="true"/> when conversion succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, IFormatProvider? provider, out ArkTypeConverterValue<T> result)
    {
        result = null!;
        if (value is null)
            return false;

        // An empty value of a Nullable<T> is null, as the nullable converters of Ark.Tools.Nodatime return.
        if (value.Length == 0 && Nullable.GetUnderlyingType(typeof(T)) is not null)
        {
            result = new ArkTypeConverterValue<T>(default(T)!);
            return true;
        }

        var converter = TypeConverterCache.Converter;
        if (!TypeConverterCache.CanConvertFromString)
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

    private static class TypeConverterCache
    {
        private static readonly Lazy<TypeConverter> _converter = new(_getConverter);
        private static readonly Lazy<bool> _canConvertFromString = new(_getCanConvertFromString);

        public static TypeConverter Converter => _converter.Value;

        public static bool CanConvertFromString => _canConvertFromString.Value;

        private static bool _getCanConvertFromString()
        {
            return _converter.Value.CanConvertFrom(typeof(string));
        }

        // Resolved on first use, so a type registered after the endpoints are mapped is still found. Lazy caches a
        // missing registration, which surfaces as the same InvalidOperationException on every conversion. A
        // Nullable<T> value converts through the converter of its underlying type, as ArkTypeConverter does.
        private static TypeConverter _getConverter()
        {
            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            try
            {
                return TypeDescriptor.GetConverterFromRegisteredType(type);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"No type converter is registered for '{type}'. Call TypeDescriptor.RegisterType<T>() for it at startup, before the type is first looked up through TypeDescriptor or its converter is added with TypeDescriptor.AddAttributes; for NodaTime types call Ark.Tools.Nodatime.NodaTimeConverter.Register()."),
                    exception);
            }
        }
    }
}

internal sealed record ArkTypeConverterParameterMetadata(string Name, Type Type);