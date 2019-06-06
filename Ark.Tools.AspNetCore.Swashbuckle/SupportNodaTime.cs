// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Swashbuckle.AspNetCore.Swagger;
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
            c.MapType<LocalDate>(() => new Schema()
            {
                Type = "string",
                Format = "date",
                Example = "2016-01-21"
            });

            c.MapType<LocalDateTime>(() => new Schema()
            {
                Type = "string",
                Format = "date-time",
                Example = "2016-01-21T15:01:01.999999999"
            });

            c.MapType<Instant>(() => new Schema()
            {
                Type = "string",
                Format = "date-time",
                Example = "2016-01-21T15:01:01.999999999Z"
            });

            c.MapType<OffsetDateTime>(() => new Schema()
            {
                Type = "string",
                Format = "date-time",
                Example = "2016-01-21T15:01:01.999999999+02:00"
            });

            c.MapType<ZonedDateTime>(() => new Schema
            {
                Type = "string",
                Example = "2016-01-21T15:01:01.999999999+02:00 Europe/Rome"
            });

            c.MapType<LocalTime>(() => new Schema()
            {
                Type = "string",
                Example = "14:01:00.999999999"
            });

            c.MapType<DateTimeZone>(() => new Schema()
            {
                Type = "string",
                Example = "Europe/Rome"
            });

            c.MapType<Period>(() => new Schema()
            {
                Type = "string",
                Example = "P1Y2M-3DT4H"
            });

			//** NULLABLE ********************************//
			c.MapType<LocalDate?>(() => new Schema()
			{
				Type = "string",
				Format = "date",
				Example = "2016-01-21"
			});

			c.MapType<LocalDateTime?>(() => new Schema()
			{
				Type = "string",
				Format = "date-time",
				Example = "2016-01-21T15:01:01.999999999"
			});

			c.MapType<Instant?>(() => new Schema()
			{
				Type = "string",
				Format = "date-time",
				Example = "2016-01-21T15:01:01.999999999Z"
			});

			c.MapType<OffsetDateTime?>(() => new Schema()
			{
				Type = "string",
				Format = "date-time",
				Example = "2016-01-21T15:01:01.999999999+02:00"
			});

			c.MapType<ZonedDateTime?>(() => new Schema
			{
				Type = "string",
				Example = "2016-01-21T15:01:01.999999999+02:00 Europe/Rome"
			});

			c.MapType<LocalTime?>(() => new Schema()
			{
				Type = "string",
				Example = "14:01:00.999999999"
			});
		}
    }
}
