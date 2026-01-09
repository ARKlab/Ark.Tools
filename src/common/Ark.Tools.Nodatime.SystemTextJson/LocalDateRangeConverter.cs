using NodaTime;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Nodatime.SystemTextJson;

public class LocalDateRangeConverter : JsonConverter<LocalDateRange>
{
    public override LocalDateRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse LocalDateTime");
        return new LocalDateRange(x.Start, x.End);
    }

    public override void Write(Utf8JsonWriter writer, LocalDateRange value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer,
            new Surrogate { Start = value.Start, End = value.End },
            options);
    }

    private sealed class Surrogate
    {
        public LocalDate Start { get; set; }
        public LocalDate End { get; set; }
    }
}