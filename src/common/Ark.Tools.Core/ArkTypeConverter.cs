// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;

namespace Ark.Tools.Core;

/// <summary>
/// Provides string-to-value conversion with per-type <see cref="TypeConverter"/> caching.
/// </summary>
public static class ArkTypeConverter
{
    /// <summary>
    /// Tries to convert <paramref name="input"/> to <typeparamref name="T"/> using a cached
    /// <see cref="TypeConverter"/> for the underlying type.
    /// </summary>
    /// <typeparam name="T">The target type, including <c>Nullable&lt;U&gt;</c> variants.</typeparam>
    /// <param name="input">The string value to convert. May be <see langword="null"/>.</param>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, contains the converted value;
    /// otherwise the default value for <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the conversion succeeded;
    /// <see langword="false"/> if <paramref name="input"/> is <see langword="null"/> and
    /// <typeparamref name="T"/> is a non-nullable value type, or if the conversion failed.
    /// </returns>
    public static bool TryConvert<T>(string? input, out T value)
    {
        if (input is null)
        {
            value = default!;
            // ponytail: relies on default(T) == null for reference types and Nullable<T>;
            // for non-nullable value types default(T) != null, so this correctly returns false.
            return default(T) is null;
        }

        try
        {
            value = ConverterCache<T>.Convert(input);
            return true;
        }
        catch (Exception ex) when (ex is FormatException
                                        or NotSupportedException
                                        or InvalidCastException
                                        or OverflowException
                                        or ArgumentException)
        {
            value = default!;
            return false;
        }
    }

    private static class ConverterCache<T>
    {
        private static readonly Func<string, T> _convert = Build();

        [UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "TryConvert<T> is called from generated code where T is a known contract scalar type preserved by the source generator.")]
        [UnconditionalSuppressMessage("Trimming", "IL2087",
            Justification = "TryConvert<T> is called from generated code where T is a known contract scalar type preserved by the source generator.")]
        private static Func<string, T> Build()
        {
            var type = typeof(T);
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string))
                return static input => (T)(object)input;

            var converter = TypeDescriptor.GetConverter(underlying);
            return input =>
            {
                var obj = converter.ConvertFromString(null, CultureInfo.InvariantCulture, input);
                return (T)obj!;
            };
        }

        public static T Convert(string input) => _convert(input);
    }
}
