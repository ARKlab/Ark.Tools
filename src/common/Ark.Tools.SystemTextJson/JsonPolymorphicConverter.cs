using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

public abstract class JsonPolymorphicConverter<TBase, TDiscriminatorEnum> : JsonConverter<TBase>
    where TDiscriminatorEnum : struct, Enum
{
    private readonly string _discriminatorPropertyName;

    protected JsonPolymorphicConverter(string discriminatorPropertyName)
    {
        _discriminatorPropertyName = discriminatorPropertyName;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1721:Property names should not match get methods", Justification = "Historical naming")]
    protected abstract Type GetType(TDiscriminatorEnum discriminatorValue);

    public override TBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        using var jsonDocument = JsonDocument.ParseValue(ref reader);
        var pn = options.PropertyNamingPolicy?.ConvertName(_discriminatorPropertyName) ?? _discriminatorPropertyName;

        if (!jsonDocument.RootElement.TryGetProperty(pn, out var typeProperty))
        {
            throw new JsonException();
        }

        Type? type = null;

        if (typeProperty.ValueKind == JsonValueKind.Number
            && typeProperty.TryGetInt32(out var enumInt)
            && Enum.IsDefined(typeof(TDiscriminatorEnum), enumInt))
        {
            type = GetType((TDiscriminatorEnum)Enum.ToObject(typeof(TDiscriminatorEnum), enumInt));
        }
        else if (typeProperty.ValueKind == JsonValueKind.String
            && Enum.TryParse<TDiscriminatorEnum>(typeProperty.GetString(), true, out var enumVal))
        {
            type = GetType(enumVal);
        }

        if (type == null)
        {
            throw new JsonException();
        }

        var jsonObject = jsonDocument.RootElement.GetRawText();
        var result = (TBase?)JsonSerializer.Deserialize(jsonObject, type, options);

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object?)value, options);
    }
}