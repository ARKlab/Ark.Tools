// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NodaTime;
using NodaTime.Calendars;


namespace Ark.Tools.Nodatime
{
    public static partial class TimezoneExtensions
    {


        public static LocalDate FirstDayOfTheWeek(this LocalDate date, IsoDayOfWeek dayOfWeek = IsoDayOfWeek.Monday)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            return LocalDate.FromWeekYearWeekAndDay(WeekYearRules.Iso.GetWeekYear(date), WeekYearRules.Iso.GetWeekOfWeekYear(date), dayOfWeek);
        }


        public static LocalDate FirstDayOfTheMonth(this LocalDate date)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            return new LocalDate(date.Year, date.Month, 1, date.Calendar);
        }


        public static LocalDate FirstDayOfTheQuarter(this LocalDate date)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            return new LocalDate(date.Year, (int)((date.Month - 1) / 3) * 3 + 1, 1, date.Calendar);
        }


        public static LocalDate FirstDayOfTheSeason(this LocalDate date)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            if (date.Month >= 10)
                return new LocalDate(date.Year, 10, 1, date.Calendar);
            else if(date.Month < 4)
                return new LocalDate(date.Year-1, 10, 1, date.Calendar);
            else 
                return new LocalDate(date.Year, 4, 1, date.Calendar);
        }


        public static LocalDate FirstDayOfTheYear(this LocalDate date)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            return new LocalDate(date.Year, 1, 1, date.Calendar);
        }


        public static LocalDate LastDayOfTheWeek(this LocalDate date, IsoDayOfWeek dayOfWeek = IsoDayOfWeek.Sunday)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            return LocalDate.FromWeekYearWeekAndDay(WeekYearRules.Iso.GetWeekYear(date), WeekYearRules.Iso.GetWeekOfWeekYear(date), dayOfWeek);
        }


        public static LocalDate LastDayOfTheMonth(this LocalDate date)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            return date.FirstDayOfTheMonth().PlusMonths(1).Minus(Period.FromDays(1));
        }


        public static LocalDate LastDayOfTheQuarter(this LocalDate date)
        {
            return date.FirstDayOfTheMonth().PlusMonths(3).Minus(Period.FromDays(1));
        }


        public static LocalDate LastDayOfTheSeason(this LocalDate date)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            if (date.Month >= 10)
                return new LocalDate(date.Year+1, 3, 31, date.Calendar);
            else if (date.Month < 4)
                return new LocalDate(date.Year, 3, 31, date.Calendar);
            else
                return new LocalDate(date.Year, 9, 30, date.Calendar);
        }


        public static LocalDate LastDayOfTheYear(this LocalDate date)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            return date.FirstDayOfTheYear().PlusYears(1).Minus(Period.FromDays(1));
        }



        public static LocalDate PreviousDayOfWeek(this LocalDate date, IsoDayOfWeek dayOfWeek = IsoDayOfWeek.Monday)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            while (date.DayOfWeek != dayOfWeek)
                date = date.PlusDays(-1);

            return date;
        }


        public static LocalDate NextDayOfWeek(this LocalDate date, IsoDayOfWeek dayOfWeek = IsoDayOfWeek.Monday)
        {
            Ensure.Bool.IsTrue(date.Calendar == CalendarSystem.Iso);
            while (date.DayOfWeek != dayOfWeek)
                date = date.PlusDays(1);

            return date;
        }
    }
}