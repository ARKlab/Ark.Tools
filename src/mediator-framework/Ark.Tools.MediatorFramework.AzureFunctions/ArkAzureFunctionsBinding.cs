// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>
/// Converts route and query string values for generated Functions with the same strategies the Minimal API host
/// uses, so a contract binds the same way on both hosts.
/// </summary>
public static class ArkAzureFunctionsBinding
{
    /// <summary>Parses a value of a type that implements <see cref="IParsable{TSelf}"/>, explicitly or not.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="value">The string value.</param>
    /// <param name="result">The parsed value when parsing succeeds.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse<T>(string? value, [MaybeNullWhen(false)] out T result)
        where T : IParsable<T>
    {
        return T.TryParse(value, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Converts a value through the converter <see cref="TypeDescriptor.GetConverter(Type)"/> returns, which includes
    /// converters added with <see cref="TypeDescriptor.AddAttributes(Type, Attribute[])"/>, such as the Ark.Tools
    /// NodaTime converters.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="value">The string value.</param>
    /// <param name="result">The converted value when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryConvert<T>(string? value, [MaybeNullWhen(false)] out T result)
    {
        result = default;
        if (value is null || !ConverterCache<T>.CanConvertFromString)
            return false;

        try
        {
            if (ConverterCache<T>.Converter.ConvertFrom(null, CultureInfo.InvariantCulture, value) is T converted)
            {
                result = converted;
                return true;
            }
        }
        catch (Exception exception) when (exception is FormatException
                                              or NotSupportedException
                                              or InvalidCastException
                                              or OverflowException
                                              or ArgumentException)
        {
            return false;
        }

        return false;
    }

    private static class ConverterCache<T>
    {
        public static readonly TypeConverter Converter = _getConverter();
        public static readonly bool CanConvertFromString = Converter.CanConvertFrom(typeof(string));

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Converters are explicitly registered by the host for the closed generated contract types.")]
        [UnconditionalSuppressMessage("Trimming", "IL2087", Justification = "Converters are explicitly registered by the host for the closed generated contract types.")]
        private static TypeConverter _getConverter()
        {
            return TypeDescriptor.GetConverter(typeof(T));
        }
    }
}
