using Ark.Reference.Common.Dto.Enum;

using NodaTime;

using System;

namespace Ark.Reference.Common
{
    public static class DateTimeExtensions
    {
        public static ZonedDateTime ValidateAtStartOfDay(this ZonedDateTime dt)
            => dt.LocalDateTime == dt.LocalDateTime.Date.AtMidnight()
            ? dt
            : throw new InvalidOperationException($"Should be at start of day: {dt}")
            ;

        public static ZonedDateTime FromInclusiveToExclusive(this ZonedDateTime dt, SourceCurveGranularity granularity)
        {
            return granularity switch
            {
                SourceCurveGranularity.Hour => dt.PlusHours(1),
                SourceCurveGranularity.Day => dt.ValidateAtStartOfDay().Date.PlusDays(1).AtStartOfDayInZone(dt.Zone),
                SourceCurveGranularity.Month => dt.ValidateAtStartOfDay().Date.PlusMonths(1).AtStartOfDayInZone(dt.Zone),
                SourceCurveGranularity.FifteenMinute => dt.PlusMinutes(15),
                _ => throw new NotSupportedException()
            };
        }

        public static OffsetDateTime FromInclusiveToExclusive(this OffsetDateTime dt, SourceCurveGranularity granularity)
        {
            return granularity switch
            {
                SourceCurveGranularity.Day => dt.Plus(Duration.FromDays(1)),
                SourceCurveGranularity.Hour => dt.PlusHours(1),
                SourceCurveGranularity.FifteenMinute => dt.PlusMinutes(15),
                _ => throw new NotSupportedException()
            };
        }
    }
}
