using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ark.Tools.Nodatime.SystemTextJson
{
    public static class Ex
    {
        public static JsonSerializerOptions ConfigureForNodaTimeRanges(this JsonSerializerOptions @this)
        {
            if (!@this.Converters.OfType<LocalDateRangeConverter>().Any())
                @this.Converters.Add(new LocalDateRangeConverter());

            if (!@this.Converters.OfType<LocalDateTimeRangeConverter>().Any())
                @this.Converters.Add(new LocalDateTimeRangeConverter());

            if (!@this.Converters.OfType<ZonedDateTimeRangeConverter>().Any())
                @this.Converters.Add(new ZonedDateTimeRangeConverter());

            return @this;
        }
    }
}
