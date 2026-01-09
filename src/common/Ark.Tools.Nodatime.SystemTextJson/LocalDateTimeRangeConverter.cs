using NodaTime;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Nodatime.SystemTextJson(net10.0)', Before:
namespace Ark.Tools.Nodatime.SystemTextJson
{
    public class LocalDateTimeRangeConverter : JsonConverter<LocalDateTimeRange>
    {
        public override LocalDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse LocalDateTimeRange");
            return new LocalDateTimeRange(x.Start, x.End);
        }

        public override void Write(Utf8JsonWriter writer, LocalDateTimeRange value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer,
                new Surrogate { Start = value.Start, End = value.End },
                options);
        }

        private sealed class Surrogate
        {
            public LocalDateTime Start { get; set; }
            public LocalDateTime End { get; set; }
        }
=======
namespace Ark.Tools.Nodatime.SystemTextJson;

public class LocalDateTimeRangeConverter : JsonConverter<LocalDateTimeRange>
{
    public override LocalDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse LocalDateTimeRange");
        return new LocalDateTimeRange(x.Start, x.End);
    }

    public override void Write(Utf8JsonWriter writer, LocalDateTimeRange value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer,
            new Surrogate { Start = value.Start, End = value.End },
            options);
    }

    private sealed class Surrogate
    {
        public LocalDateTime Start { get; set; }
        public LocalDateTime End { get; set; }
>>>>>>> After


namespace Ark.Tools.Nodatime.SystemTextJson;

    public class LocalDateTimeRangeConverter : JsonConverter<LocalDateTimeRange>
    {
        public override LocalDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse LocalDateTimeRange");
            return new LocalDateTimeRange(x.Start, x.End);
        }

        public override void Write(Utf8JsonWriter writer, LocalDateTimeRange value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer,
                new Surrogate { Start = value.Start, End = value.End },
                options);
        }

        private sealed class Surrogate
        {
            public LocalDateTime Start { get; set; }
            public LocalDateTime End { get; set; }
        }
    }