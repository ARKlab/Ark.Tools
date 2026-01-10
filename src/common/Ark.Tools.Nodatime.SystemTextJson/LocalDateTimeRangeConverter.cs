using NodaTime;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Nodatime.SystemTextJson;

public class LocalDateTimeRangeConverter : JsonConverter<LocalDateTimeRange>
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The Surrogate type is a private class defined in this converter with only LocalDateTime properties. NodaTime types are explicitly supported by NodaTime.Serialization.SystemTextJson and will not be trimmed.")]
    public override LocalDateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var x = JsonSerializer.Deserialize<Surrogate>(ref reader, options) ?? throw new JsonException("cannot parse LocalDateTimeRange");
        return new LocalDateTimeRange(x.Start, x.End);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The Surrogate type is a private class defined in this converter with only LocalDateTime properties. NodaTime types are explicitly supported by NodaTime.Serialization.SystemTextJson and will not be trimmed.")]
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