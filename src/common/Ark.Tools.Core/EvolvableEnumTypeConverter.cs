// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Ark.Tools.Core;

/// <summary>Converts evolvable enum values from and to strings and their exact backing type.</summary>
public sealed class EvolvableEnumTypeConverter : TypeConverter
{
    private readonly Type _wrapperType;
    private readonly Type _backingType;
    private readonly MethodInfo _parse;
    private readonly MethodInfo _fromNumber;
    private readonly MethodInfo _toNumber;

    /// <summary>Initializes a converter for a closed evolvable enum type.</summary>
    /// <param name="type">The closed evolvable enum type.</param>
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "The closed wrapper's public parsing and conversion methods are part of its public API.")]
    public EvolvableEnumTypeConverter(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var arguments = type.GetGenericArguments();
        if (!type.IsGenericType || arguments.Length is < 1 or > 2)
            throw new ArgumentException("The type must be a closed EvolvableEnum type.", nameof(type));

        _wrapperType = type;
        _backingType = arguments.Length == 1 ? typeof(int) : arguments[1];
        _parse = type.GetMethod("Parse", [typeof(string)])!;
        _fromNumber = type.GetMethod("FromNumber", [_backingType])!;
        _toNumber = type.GetMethod("ToNumber", Type.EmptyTypes)!;
    }

    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == _backingType || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || destinationType == _backingType || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is string text)
            return _parse.Invoke(null, [text]);
        if (value.GetType() == _backingType)
            return _fromNumber.Invoke(null, [value]);

        return base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc />
    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(destinationType);
        if (value is not null && value.GetType() == _wrapperType)
        {
            if (destinationType == typeof(string))
                return value.ToString();
            if (destinationType == _backingType)
            {
                try
                {
                    return _toNumber.Invoke(value, null);
                }
                catch (TargetInvocationException exception) when (exception.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                }
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
