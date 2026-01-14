// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using System.ComponentModel;

namespace Ark.Tools.Nodatime;

public static class NodeTimeConverter
{
    static NodeTimeConverter()
    {
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

    public static void Register()
    {
        // Register once done using static ctor. this is here just to unsure NodeTimeConverter gets "contructed"
    }
}