using NodaTime;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Nodatime.SystemTextJson(net10.0)', Before:
namespace Ark.Tools.Nodatime.SystemTextJson
{
    public class ZonedDateTimeRangeConverter : JsonConverter<ZonedDateTimeRange>
    {
        public override ZonedDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse ZonedDateTimeRange");
            return new ZonedDateTimeRange(x.Start, x.End);
        }

        public override void Write(Utf8JsonWriter writer, ZonedDateTimeRange value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer,
                new Surrogate { Start = value.Start, End = value.End },
                options);
        }

        private sealed class Surrogate
        {
            public ZonedDateTime Start { get; set; }
            public ZonedDateTime End { get; set; }
        }
=======
namespace Ark.Tools.Nodatime.SystemTextJson;

public class ZonedDateTimeRangeConverter : JsonConverter<ZonedDateTimeRange>
{
    public override ZonedDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse ZonedDateTimeRange");
        return new ZonedDateTimeRange(x.Start, x.End);
    }

    public override void Write(Utf8JsonWriter writer, ZonedDateTimeRange value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer,
            new Surrogate { Start = value.Start, End = value.End },
            options);
    }

    private sealed class Surrogate
    {
        public ZonedDateTime Start { get; set; }
        public ZonedDateTime End { get; set; }
>>>>>>> After


namespace Ark.Tools.Nodatime.SystemTextJson;

    public class ZonedDateTimeRangeConverter : JsonConverter<ZonedDateTimeRange>
    {
        public override ZonedDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse ZonedDateTimeRange");
            return new ZonedDateTimeRange(x.Start, x.End);
        }

        public override void Write(Utf8JsonWriter writer, ZonedDateTimeRange value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer,
                new Surrogate { Start = value.Start, End = value.End },
                options);
        }

        private sealed class Surrogate
        {
            public ZonedDateTime Start { get; set; }
            public ZonedDateTime End { get; set; }
        }
    }