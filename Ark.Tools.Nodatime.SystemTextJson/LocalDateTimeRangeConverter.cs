using NodaTime;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Nodatime.SystemTextJson
{
    public class LocalDateTimeRangeConverter : JsonConverter<LocalDateTimeRange>
    {
        public override LocalDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options);
            return new LocalDateTimeRange(x.Start, x.End);
        }

        public override void Write(Utf8JsonWriter writer, LocalDateTimeRange value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer,
                new Surrogate { Start = value.Start, End = value.End },
                typeof(Surrogate),
                options);
        }

        private class Surrogate
        {
            public LocalDateTime Start { get; set; }
            public LocalDateTime End { get; set; }
        }
    }
}
