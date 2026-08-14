// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using System.Text.Json.Nodes;

namespace Ark.Tools.AspNetCore.Swashbuckle;

public static class SupportNodaTimeExtensions
{
    /// <summary>
    /// Register support for NodaTime types. For Json 
    /// </summary>
    /// <param name="c"></param>
    public static void MapNodaTimeTypes(this SwaggerGenOptions c)
    {
        c.MapType<LocalDate>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String,
            Format = "date",
            Examples = [JsonValue.Create("2016-01-21")]
        });

        c.MapType<LocalDateTime>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String,
            Format = "date-time",
            Examples = [JsonValue.Create("2016-01-21T15:01:01.999999999")]
        });

        c.MapType<Instant>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String,
            Format = "date-time",
            Examples = [JsonValue.Create("2016-01-21T15:01:01.999999999Z")]
        });

        c.MapType<OffsetDateTime>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String,
            Format = "date-time",
            Examples = [JsonValue.Create("2016-01-21T15:01:01.999999999+02:00")]
        });

        c.MapType<ZonedDateTime>(() => new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Examples = [JsonValue.Create("2016-01-21T15:01:01.999999999+02:00 Europe/Rome")]
        });

        c.MapType<LocalTime>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String,
            Format = "time",
            Examples = [JsonValue.Create("14:01:00.999999999")]
        });

        c.MapType<DateTimeZone>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String,
            Examples = [JsonValue.Create("Europe/Rome")]
        });

        c.MapType<Period>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String,
            Format = "duration",
            Examples = [JsonValue.Create("P1Y2M-3DT4H")]
        });

        //** NULLABLE ********************************//
        c.MapType<LocalDate?>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Format = "date",
            Examples = [JsonValue.Create("2016-01-21")]
        });

        c.MapType<LocalDateTime?>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Format = "date-time",
            Examples = [JsonValue.Create("2016-01-21T15:01:01.999999999")]
        });

        c.MapType<Instant?>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Format = "date-time",
            Examples = [JsonValue.Create("2016-01-21T15:01:01.999999999Z")]
        });

        c.MapType<OffsetDateTime?>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Format = "date-time",
            Examples = [JsonValue.Create("2016-01-21T15:01:01.999999999+02:00")]
        });

        c.MapType<ZonedDateTime?>(() => new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Examples = [JsonValue.Create("2016-01-21T15:01:01.999999999+02:00 Europe/Rome")]
        });

        c.MapType<LocalTime?>(() => new OpenApiSchema()
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Format = "time",
            Examples = [JsonValue.Create("14:01:00.999999999")]
        });
    }
}