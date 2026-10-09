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
    [RequiresUnreferencedCode("TypeDescriptor.GetConverter is not trim-safe. Ensure T and its TypeConverter are preserved.")]
    public static bool TryConvert<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(string? input, out T value)
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

    /// <summary>
    /// Tries to convert <paramref name="input"/> to <typeparamref name="T"/> using a cached
    /// <see cref="TypeConverter"/> obtained via <c>TypeDescriptor.GetConverterFromRegisteredType</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is trim-safe: it calls <c>TypeDescriptor.GetConverterFromRegisteredType</c>, which only
    /// considers primitive types, enums and types registered with <c>TypeDescriptor.RegisterType</c>, and does
    /// not perform reflection-based discovery.
    /// </para>
    /// <para>
    /// Register a type at application start, before the type is first looked up through <c>TypeDescriptor</c> or its
    /// converter is added with <c>TypeDescriptor.AddAttributes</c>: a later registration is ignored. For NodaTime types call
    /// <c>Ark.Tools.Nodatime.NodaTimeConverter.Register()</c>. Converting to an unregistered type throws
    /// <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
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
    public static bool TryConvertSafe<T>(string? input, out T value)
    {
        if (input is null)
        {
            value = default!;
            // ponytail: same null-handling contract as TryConvert<T>.
            return default(T) is null;
        }

        try
        {
            value = ConverterCacheSafe<T>.Convert(input);
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

    [RequiresUnreferencedCode("TypeDescriptor.GetConverter is not trim-safe. Ensure T and its TypeConverter are preserved.")]
    private static class ConverterCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    {
        private static readonly Func<string, T> _convert = _build();

        private static Func<string, T> _build()
        {
            var type = typeof(T);
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string))
            {
                return static input =>
                {
                    if (input is T typed)
                        return typed;

                    throw new InvalidCastException($"Unable to convert {typeof(string)} to {typeof(T)}.");
                };
            }

            var converter = TypeDescriptor.GetConverter(underlying);
            return input =>
            {
                var obj = converter.ConvertFromString(null, CultureInfo.InvariantCulture, input);
                return (T)obj!;
            };
        }

        public static T Convert(string input) => _convert(input);
    }

    private static class ConverterCacheSafe<T>
    {
        private static readonly Func<string, T> _convert = _build();

        private static Func<string, T> _build()
        {
            var type = typeof(T);
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string))
            {
                return static input =>
                {
                    if (input is T typed)
                        return typed;

                    throw new InvalidCastException($"Unable to convert {typeof(string)} to {typeof(T)}.");
                };
            }

            TypeConverter converter;
            try
            {
                converter = TypeDescriptor.GetConverterFromRegisteredType(underlying);
            }
            catch (InvalidOperationException exception)
            {
                // Report the missing registration on every conversion instead of failing the type initializer.
                var message = string.Create(CultureInfo.InvariantCulture,
                    $"No type converter is registered for '{underlying}'. Call TypeDescriptor.RegisterType<T>() for it at startup, before the type is first looked up through TypeDescriptor or its converter is added with TypeDescriptor.AddAttributes; for NodaTime types call Ark.Tools.Nodatime.NodaTimeConverter.Register().");
                return _ => throw new InvalidOperationException(message, exception);
            }

            return input =>
            {
                var obj = converter.ConvertFromString(null, CultureInfo.InvariantCulture, input);
                return (T)obj!;
            };
        }

        public static T Convert(string input) => _convert(input);
    }
}
