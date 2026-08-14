// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using System.Text.Json.Nodes;

namespace Ark.Tools.AspNetCore.Swashbuckle;

public static class SupportNodaTimeExtensions
{
    /// <summary>
    /// Register support for NodaTime types.
    /// </summary>
    /// <param name="c">The Swagger generator options.</param>
    public static void MapNodaTimeTypes(this SwaggerGenOptions c)
    {
        c.MapType<LocalDate>(() => _schema("date", "2016-01-21"));
        c.MapType<LocalDateTime>(() => _schema("date-time", "2016-01-21T15:01:01.999999999"));
        c.MapType<Instant>(() => _schema("date-time", "2016-01-21T15:01:01.999999999Z"));
        c.MapType<OffsetDateTime>(() => _schema("date-time", "2016-01-21T15:01:01.999999999+02:00"));
        c.MapType<ZonedDateTime>(() => _schema(null, "2016-01-21T15:01:01.999999999+02:00 Europe/Rome"));
        c.MapType<LocalTime>(() => _schema("time", "14:01:00.999999999"));
        c.MapType<DateTimeZone>(() => _schema(null, "Europe/Rome"));
        c.MapType<Period>(() => _schema("duration", "P1Y2M-3DT4H"));

        //** NULLABLE ********************************//
        c.MapType<LocalDate?>(() => _schema("date", "2016-01-21", nullable: true));
        c.MapType<LocalDateTime?>(() => _schema("date-time", "2016-01-21T15:01:01.999999999", nullable: true));
        c.MapType<Instant?>(() => _schema("date-time", "2016-01-21T15:01:01.999999999Z", nullable: true));
        c.MapType<OffsetDateTime?>(() => _schema("date-time", "2016-01-21T15:01:01.999999999+02:00", nullable: true));
        c.MapType<ZonedDateTime?>(() => _schema(null, "2016-01-21T15:01:01.999999999+02:00 Europe/Rome", nullable: true));
        c.MapType<LocalTime?>(() => _schema("time", "14:01:00.999999999", nullable: true));
    }

    private static OpenApiSchema _schema(string? format, string example, bool nullable = false)
    {
        return new OpenApiSchema
        {
            Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String,
            Format = format,
            Examples = [JsonValue.Create(example)]
        };
    }
}