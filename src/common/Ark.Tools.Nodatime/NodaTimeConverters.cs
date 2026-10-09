// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using System.ComponentModel;

namespace Ark.Tools.Nodatime;

/// <summary>Registers the Ark.Tools type converters for NodaTime values with <see cref="TypeDescriptor"/>.</summary>
/// <remarks>
/// Each type is registered with <see cref="TypeDescriptor.RegisterType{T}"/> before its converter is added, so both
/// <see cref="TypeDescriptor.GetConverter(Type)"/> and the trim-safe
/// <see cref="TypeDescriptor.GetConverterFromRegisteredType(Type)"/> return it. A converter added before the type is
/// registered is not visible to <see cref="TypeDescriptor.GetConverterFromRegisteredType(Type)"/>.
/// </remarks>
public static class NodeTimeConverter
{
    static NodeTimeConverter()
    {
        TypeDescriptor.RegisterType<LocalDate>();
        TypeDescriptor.RegisterType<LocalTime>();
        TypeDescriptor.RegisterType<LocalDateTime>();
        TypeDescriptor.RegisterType<Instant>();
        TypeDescriptor.RegisterType<OffsetDateTime>();
        TypeDescriptor.RegisterType<LocalDate?>();
        TypeDescriptor.RegisterType<LocalTime?>();
        TypeDescriptor.RegisterType<LocalDateTime?>();
        TypeDescriptor.RegisterType<Instant?>();
        TypeDescriptor.RegisterType<OffsetDateTime?>();

        TypeDescriptor.AddAttributes(typeof(LocalDate), new TypeConverterAttribute(typeof(LocalDateConverter)));
        TypeDescriptor.AddAttributes(typeof(LocalTime), new TypeConverterAttribute(typeof(LocalTimeConverter)));
        TypeDescriptor.AddAttributes(typeof(LocalDateTime), new TypeConverterAttribute(typeof(LocalDateTimeConverter)));
        TypeDescriptor.AddAttributes(typeof(Instant), new TypeConverterAttribute(typeof(InstantConverter)));
        TypeDescriptor.AddAttributes(typeof(OffsetDateTime), new TypeConverterAttribute(typeof(OffsetDateTimeConverter)));
        TypeDescriptor.AddAttributes(typeof(LocalDate?), new TypeConverterAttribute(typeof(NullableLocalDateConverter)));
        TypeDescriptor.AddAttributes(typeof(LocalTime?), new TypeConverterAttribute(typeof(NullableLocalTimeConverter)));
        TypeDescriptor.AddAttributes(typeof(LocalDateTime?), new TypeConverterAttribute(typeof(NullableLocalDateTimeConverter)));
        TypeDescriptor.AddAttributes(typeof(Instant?), new TypeConverterAttribute(typeof(NullableInstantConverter)));
        TypeDescriptor.AddAttributes(typeof(OffsetDateTime?), new TypeConverterAttribute(typeof(NullableOffsetDateTimeConverter)));
    }

    /// <summary>Registers the NodaTime types and their converters once; later calls do nothing.</summary>
    public static void Register()
    {
        // Register once done using static ctor. this is here just to unsure NodeTimeConverter gets "contructed"
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