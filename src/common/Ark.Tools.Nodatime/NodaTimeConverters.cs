// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using System.ComponentModel;

namespace Ark.Tools.Nodatime;

/// <summary>Registers the Ark.Tools type converters for NodaTime values with <see cref="TypeDescriptor"/>.</summary>
/// <remarks>
/// Each converter is added as a <see cref="TypeConverterAttribute"/>, which <see cref="TypeDescriptor.GetConverter(Type)"/>
/// returns, and through a <see cref="TypeDescriptionProvider"/> that reports the type as registered, which the trim-safe
/// <see cref="TypeDescriptor.GetConverterFromRegisteredType(Type)"/> returns. Unlike
/// <see cref="TypeDescriptor.RegisterType{T}"/>, the provider works even when the type was already looked up through
/// <see cref="TypeDescriptor"/>, for example by <see cref="TypeDescriptor.GetConverter(Type)"/>.
/// </remarks>
public static class NodeTimeConverter
{
    static NodeTimeConverter()
    {
        _add<LocalDate, LocalDateConverter>();
        _add<LocalTime, LocalTimeConverter>();
        _add<LocalDateTime, LocalDateTimeConverter>();
        _add<Instant, InstantConverter>();
        _add<OffsetDateTime, OffsetDateTimeConverter>();
        _add<LocalDate?, NullableLocalDateConverter>();
        _add<LocalTime?, NullableLocalTimeConverter>();
        _add<LocalDateTime?, NullableLocalDateTimeConverter>();
        _add<Instant?, NullableInstantConverter>();
        _add<OffsetDateTime?, NullableOffsetDateTimeConverter>();
    }

    /// <summary>Registers the NodaTime types and their converters once; later calls do nothing.</summary>
    public static void Register()
    {
        // Register once done using static ctor. this is here just to unsure NodeTimeConverter gets "contructed"
    }

    private static void _add<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConverter>()
        where TConverter : TypeConverter, new()
    {
        TypeDescriptor.AddAttributes(typeof(T), new TypeConverterAttribute(typeof(TConverter)));
        TypeDescriptor.AddProvider(new ConverterProvider<TConverter>(typeof(T)), typeof(T));
    }

    private sealed class ConverterProvider<TConverter> : TypeDescriptionProvider
        where TConverter : TypeConverter, new()
    {
        private readonly Type _type;

        public ConverterProvider(Type type)
            : base(TypeDescriptor.GetProvider(type))
        {
            _type = type;
        }

        public override bool IsRegisteredType(Type type)
        {
            return type == _type || base.IsRegisteredType(type);
        }

        public override ICustomTypeDescriptor? GetTypeDescriptorFromRegisteredType(Type objectType, object? instance)
        {
            return objectType == _type ? new ConverterDescriptor<TConverter>() : base.GetTypeDescriptorFromRegisteredType(objectType, instance);
        }
    }

    private sealed class ConverterDescriptor<TConverter> : CustomTypeDescriptor
        where TConverter : TypeConverter, new()
    {
        // One stateless converter per type, as TypeDescriptor caches it: Dapper handlers look it up for every value.
        private static readonly TConverter _converter = new();

        public override TypeConverter GetConverterFromRegisteredType()
        {
            return _converter;
        }
    }
}

/// <summary>Registers Ark.Tools type converters for NodaTime values.</summary>
/// <remarks>
/// Call <see cref="Register"/> once at startup, before NodaTime values are converted from strings: HTTP route and
/// query binding, Dapper type handlers and JSON dictionary keys resolve the converters with
/// <see cref="TypeDescriptor.GetConverterFromRegisteredType(Type)"/>, which only sees registered types.
/// </remarks>
public static class NodaTimeConverter
{
    /// <summary>Registers the NodaTime types and their type converters with <see cref="TypeDescriptor"/>.</summary>
    public static void Register()
    {
        NodeTimeConverter.Register();
    }
}
