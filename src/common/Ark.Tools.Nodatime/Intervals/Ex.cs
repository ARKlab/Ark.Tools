// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;


namespace Ark.Tools.Nodatime.Intervals;

public static class Ex
{
    public static Period ToPeriod(DatePeriod datePeriod)
    {
        switch (datePeriod)
        {
            case DatePeriod.Day:
                return Period.FromDays(1);
            case DatePeriod.Week:
                return Period.FromDays(7);
            case DatePeriod.Month:
                return Period.FromMonths(1);
            case DatePeriod.Bimestral:
                return Period.FromMonths(2);
            case DatePeriod.Trimestral:
                return Period.FromMonths(3);
            case DatePeriod.Calendar:
                return Period.FromYears(1);
        }

        throw new NotSupportedException();
    }

    public static Duration ToPeriod(TimePeriod timePeriod)
    {
        switch (timePeriod)
        {
            case TimePeriod.Hour:
                return Duration.FromHours(1);
            case TimePeriod.TenMinutes:
                return Duration.FromMinutes(10);
            case TimePeriod.Minute:
                return Duration.FromMinutes(1);
            case TimePeriod.QuarterHour:
                return Duration.FromMinutes(15);
            case TimePeriod.HalfHour:
                return Duration.FromMinutes(30);
        }

        throw new NotSupportedException();
    }
}