using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

public class NullableStructSerializer<TStruct> : JsonConverter<TStruct?>
    where TStruct : struct
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "JSON serialization of TStruct is constrained to value types. The converter is instantiated by NullableStructSerializerFactory which ensures TStruct will be preserved.")]
    public override TStruct? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        return JsonSerializer.Deserialize<TStruct>(ref reader, options);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "JSON serialization of TStruct is constrained to value types. The converter is instantiated by NullableStructSerializerFactory which ensures TStruct will be preserved.")]
    public override void Write(Utf8JsonWriter writer, TStruct? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, value.Value, options);
    }
}