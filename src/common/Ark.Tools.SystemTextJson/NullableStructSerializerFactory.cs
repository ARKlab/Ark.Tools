using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

public class NullableStructSerializerFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var t = Nullable.GetUnderlyingType(typeToConvert);
        return t != null;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2055:MakeGenericType",
        Justification = "The struct type is derived from typeToConvert which is a nullable value type known at runtime. NullableStructSerializer<T> will be preserved.")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var structType = typeToConvert.GenericTypeArguments[0];

        return (JsonConverter?)Activator.CreateInstance(typeof(NullableStructSerializer<>).MakeGenericType(structType));
    }
}