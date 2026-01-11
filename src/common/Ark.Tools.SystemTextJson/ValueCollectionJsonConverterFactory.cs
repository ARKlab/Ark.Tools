using Ark.Tools.Core;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

public class ValueCollectionJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(ValueCollection<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var elementType = typeToConvert.GetGenericArguments()[0];

        var converterType = typeof(Converter<>);

        var converter = (JsonConverter)Activator.CreateInstance(
            converterType.MakeGenericType(elementType),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: null,
            culture: null)!;

        return converter;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "CA1812", Justification = "Instantiated via reflection in CreateConverter method")]
    private sealed class Converter<T> : JsonConverter<ValueCollection<T>>
    {
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "The element type T comes from ValueCollection<T> which is instantiated by the factory. The factory ensures T is preserved.")]
        public override ValueCollection<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException();
            }

            reader.Read();

            ValueCollection<T> elements = new();

            while (reader.TokenType != JsonTokenType.EndArray)
            {
                var value = JsonSerializer.Deserialize<T>(ref reader, options);
                elements.Add(value);

                reader.Read();
            }

            return elements;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "The element type T comes from ValueCollection<T> which is instantiated by the factory. The factory ensures T is preserved.")]
        public override void Write(Utf8JsonWriter writer, ValueCollection<T> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.AsEnumerable(), options);
        }
    }
}