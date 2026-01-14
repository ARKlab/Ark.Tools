using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

/// <summary>
/// JsonConverter for nullable structs with custom converters.
/// </summary>
/// <remarks>
/// This converter provides explicit handling for nullable value types (T?) where T has a custom JsonConverter.
/// While .NET 8+ has improved nullable struct support, this converter ensures consistent behavior
/// and proper null handling for all scenarios.
/// </remarks>
/// <typeparam name="TStruct">The nullable value type to convert.</typeparam>
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