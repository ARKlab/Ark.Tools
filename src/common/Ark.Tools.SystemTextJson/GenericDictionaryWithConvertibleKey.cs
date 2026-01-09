using Ark.Tools.Core;

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

public sealed class GenericDictionaryWithConvertibleKey : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        Type? actualTypeToConvert;
        Type? keyType = null;
        if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericBaseClass(typeof(Dictionary<,>))) != null)
        {
            keyType = actualTypeToConvert.GetGenericArguments()[0];
        }
        else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IDictionary<,>))) != null)
        {
            keyType = actualTypeToConvert.GetGenericArguments()[0];
        }
        else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IReadOnlyDictionary<,>))) != null)
        {
            keyType = actualTypeToConvert.GetGenericArguments()[0];
        }

        if (keyType != null && keyType != typeof(string))
        {
            var converter = TypeDescriptor.GetConverter(keyType);
            return converter.CanConvertFrom(typeof(string)) && converter.CanConvertTo(typeof(string));
        }

        return false;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type? actualTypeToConvert;
        Type? converterType = null;
        if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericBaseClass(typeof(Dictionary<,>))) != null)
        {
            var args = actualTypeToConvert.GetGenericArguments();
            var keyType = args[0];
            var elementType = args[1];

            converterType = typeof(DictionaryBaseConverter<,,>)
                .MakeGenericType(typeToConvert, keyType, elementType);
        }
        else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IDictionary<,>))) != null)
        {
            var args = actualTypeToConvert.GetGenericArguments();
            var keyType = args[0];
            var elementType = args[1];

            if (actualTypeToConvert == typeToConvert)
                converterType = typeof(DictionaryConverter<,>)
                    .MakeGenericType(keyType, elementType);
            else
                converterType = typeof(IDictionaryBaseConverter<,,>)
                    .MakeGenericType(typeToConvert, keyType, elementType);
        }
        else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IReadOnlyDictionary<,>))) != null)
        {
            var args = actualTypeToConvert.GetGenericArguments();
            var keyType = args[0];
            var elementType = args[1];

            converterType = typeof(ReadOnlyDictionaryConverter<,>)
                    .MakeGenericType(keyType, elementType);
        }

        if (converterType != null)
            return (JsonConverter?)Activator.CreateInstance(converterType, options);

        return null;
    }
}