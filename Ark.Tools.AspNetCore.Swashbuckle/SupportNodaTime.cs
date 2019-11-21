// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using NodaTime;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
	public static class SupportNodaTimeEx
    { 
        /// <summary>
        /// Register support for NodaTime types. For Json 
        /// </summary>
        /// <param name="c"></param>
        public static void MapNodaTimeTypes(this SwaggerGenOptions c)
        {
            c.MapType<LocalDate>(() => new OpenApiSchema()
            {
                Type = "string",
                Format = "date",		
                Example = new OpenApiString("2016-01-21")
            });

            c.MapType<LocalDateTime>(() => new OpenApiSchema()
            {
                Type = "string",
                Format = "date-time",
                Example = new OpenApiString("2016-01-21T15:01:01.999999999")
			});

            c.MapType<Instant>(() => new OpenApiSchema()
            {
                Type = "string",
                Format = "date-time",
                Example = new OpenApiString("2016-01-21T15:01:01.999999999Z")
			});

            c.MapType<OffsetDateTime>(() => new OpenApiSchema()
            {
                Type = "string",
                Format = "date-time",
                Example = new OpenApiString("2016-01-21T15:01:01.999999999+02:00")
			});

            c.MapType<ZonedDateTime>(() => new OpenApiSchema
			{
                Type = "string",
                Example = new OpenApiString("2016-01-21T15:01:01.999999999+02:00 Europe/Rome")
			});

            c.MapType<LocalTime>(() => new OpenApiSchema()
            {
                Type = "string",
                Example = new OpenApiString("14:01:00.999999999")
			});

            c.MapType<DateTimeZone>(() => new OpenApiSchema()
            {
                Type = "string",
                Example = new OpenApiString("Europe/Rome")
			});

            c.MapType<Period>(() => new OpenApiSchema()
            {
                Type = "string",
                Example = new OpenApiString("P1Y2M-3DT4H")
			});

			//** NULLABLE ********************************//
			c.MapType<LocalDate?>(() => new OpenApiSchema()
			{
				Type = "string",
				Format = "date",
				Example = new OpenApiString("2016-01-21")
			});

			c.MapType<LocalDateTime?>(() => new OpenApiSchema()
			{
				Type = "string",
				Format = "date-time",
				Example = new OpenApiString("2016-01-21T15:01:01.999999999")
			});

			c.MapType<Instant?>(() => new OpenApiSchema()
			{
				Type = "string",
				Format = "date-time",
				Example = new OpenApiString("2016-01-21T15:01:01.999999999Z")
			});

			c.MapType<OffsetDateTime?>(() => new OpenApiSchema()
			{
				Type = "string",
				Format = "date-time",
				Example = new OpenApiString("2016-01-21T15:01:01.999999999+02:00")
			});

			c.MapType<ZonedDateTime?>(() => new OpenApiSchema
			{
				Type = "string",
				Example = new OpenApiString("2016-01-21T15:01:01.999999999+02:00 Europe/Rome")
			});

			c.MapType<LocalTime?>(() => new OpenApiSchema()
			{
				Type = "string",
				Example = new OpenApiString("14:01:00.999999999")
			});
		}
    }
}
