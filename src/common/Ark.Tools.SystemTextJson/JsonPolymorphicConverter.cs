using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

/// <summary>
/// Base class for polymorphic JSON deserialization using a discriminator property.
/// </summary>
/// <remarks>
/// This converter enables polymorphic deserialization by reading a discriminator property from JSON
/// and using it to determine the concrete type to deserialize. Derived classes implement the GetType
/// method to map discriminator values to concrete types.
/// </remarks>
/// <typeparam name="TBase">The base type or interface for polymorphic deserialization.</typeparam>
/// <typeparam name="TDiscriminatorEnum">The enum type used as discriminator values.</typeparam>
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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The concrete types are determined by the abstract GetType method which derived converters implement. Consumers are responsible for ensuring those types are preserved.")]
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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The value's runtime type is determined at runtime. Consumers are responsible for ensuring the polymorphic types are preserved.")]
    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object?)value, options);
    }
}