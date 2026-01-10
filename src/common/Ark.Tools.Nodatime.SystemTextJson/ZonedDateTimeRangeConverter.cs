using NodaTime;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Nodatime.SystemTextJson;

public class ZonedDateTimeRangeConverter : JsonConverter<ZonedDateTimeRange>
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The Surrogate type is a private class defined in this converter with only ZonedDateTime properties. NodaTime types are explicitly supported by NodaTime.Serialization.SystemTextJson and will not be trimmed.")]
    public override ZonedDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse ZonedDateTimeRange");
        return new ZonedDateTimeRange(x.Start, x.End);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The Surrogate type is a private class defined in this converter with only ZonedDateTime properties. NodaTime types are explicitly supported by NodaTime.Serialization.SystemTextJson and will not be trimmed.")]
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