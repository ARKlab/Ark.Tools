// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System;
using System.Collections.Generic;

namespace Ark.Tools.Nodatime
{
    public static partial class TimezoneExtensions
    {
        public static int NumberOfHoursInDay(this LocalDate date, string timezoneName)
        {
            var timezone = DateTimeZoneProviders.Tzdb[timezoneName];
            return date.NumberOfHoursInDay(timezone);
        }
        public static int NumberOfHoursInDay(this LocalDate date, DateTimeZone timezone)
        {
            ArgumentNullException.ThrowIfNull(timezone);
            return (int)(date.PlusDays(1).AtMidnight().InZoneStrictly(timezone).ToInstant() - date.AtMidnight().InZoneStrictly(timezone).ToInstant()).ToTimeSpan().TotalHours;
        }

        public static ZonedDateTime FromInstantToTimezone(this Instant instant, string timezoneName)
        {
            var timezone = DateTimeZoneProviders.Tzdb[timezoneName];
            return instant.FromInstantToTimezone(timezone);
        }

        public static ZonedDateTime FromInstantToTimezone(this Instant instant, DateTimeZone timezone)
        {
            ArgumentNullException.ThrowIfNull(timezone);

            return instant.InZone(timezone);
        }

        public static ZonedDateTime InZoneLeniently(this LocalDateTime dateTime, string timezoneName)
        {
            var timezone = DateTimeZoneProviders.Tzdb[timezoneName];
            return dateTime.InZoneLeniently(timezone);
        }

        public static ZonedDateTime InZoneStrictly(this LocalDateTime dateTime, string timezoneName)
        {
            var timezone = DateTimeZoneProviders.Tzdb[timezoneName];
            return dateTime.InZoneStrictly(timezone);
        }

        public static IEnumerable<ZonedDateTime> InZoneAllOrNone(this LocalDateTime dateTime, string timezoneName)
        {
            var timezone = DateTimeZoneProviders.Tzdb[timezoneName];
            return dateTime.InZoneAllOrNone(timezone);
        }

        public static IEnumerable<ZonedDateTime> InZoneAllOrNone(this LocalDateTime dateTime, DateTimeZone timezone)
        {
            ArgumentNullException.ThrowIfNull(timezone);

            var map = timezone.MapLocal(dateTime);
            var res = new ZonedDateTime[map.Count];
            switch (map.Count)
            {
                case 1:
                    res[0] = map.Single();
                    break;
                case 2:
                    res[0] = map.First();
                    res[1] = map.Last();
                    break;
            }
            return res;
        }

        public static ZonedDateTime FromUtcToTimezone(this LocalDateTime localUtc, string timezoneName)
        {
            var timezone = DateTimeZoneProviders.Tzdb[timezoneName];
            return localUtc.InUtc().WithZone(timezone);
        }

        public static ZonedDateTime FromUtcToTimezone(this LocalDateTime localUtc, DateTimeZone timezone)
        {
            ArgumentNullException.ThrowIfNull(timezone);
            return localUtc.InUtc().WithZone(timezone);
        }

        public static DateTime FromUtcToTimezone(this DateTime dateTime, string timezoneName)
        {
            var timezone = DateTimeZoneProviders.Tzdb[timezoneName];
            return dateTime.FromUtcToTimezone(timezone);
        }

        public static DateTime FromUtcToTimezone(this DateTime dateTime, DateTimeZone timezone)
        {
            ArgumentNullException.ThrowIfNull(timezone);

            if (dateTime.Kind != DateTimeKind.Utc)
                dateTime = new DateTime(dateTime.Ticks, DateTimeKind.Utc);
            Instant instant = Instant.FromDateTimeUtc(dateTime);

            var usersZonedDateTime = instant.InZone(timezone);
            return usersZonedDateTime.ToDateTimeUnspecified();
        }

        public static DateTime FromTimezoneToUtc(this DateTime dateTime, string timezoneName)
        {
            var timezone = DateTimeZoneProviders.Tzdb[timezoneName];
            return dateTime.FromTimezoneToUtc(timezone);
        }

        public static DateTime FromTimezoneToUtc(this DateTime dateTime, DateTimeZone timezone)
        {
            ArgumentNullException.ThrowIfNull(timezone);

            LocalDateTime localDateTime = LocalDateTime.FromDateTime(dateTime);

            var zonedDbDateTime = timezone.AtLeniently(localDateTime);
            return zonedDbDateTime.ToDateTimeUtc();
        }
    }
}