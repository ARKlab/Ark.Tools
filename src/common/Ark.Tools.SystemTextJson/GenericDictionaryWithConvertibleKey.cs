using Ark.Tools.Core;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

public sealed class GenericDictionaryWithConvertibleKey : JsonConverterFactory
{
#if NET9_0_OR_GREATER
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
            var converter = TypeDescriptor.GetConverterFromRegisteredType(keyType);
            return converter.CanConvertFrom(typeof(string)) && converter.CanConvertTo(typeof(string));
        }

        return false;
    }
#else
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "For .NET 8 and earlier, TypeConverter discovery uses reflection. In .NET 9+, use TypeDescriptor.RegisterType to ensure converters are preserved. See migration-v6.md for details.")]
    [UnconditionalSuppressMessage("Trimming", "IL2062:DynamicallyAccessedMembers",
        Justification = "For .NET 8 and earlier, TypeConverter discovery uses reflection. In .NET 9+, use TypeDescriptor.RegisterType to ensure converters are preserved. See migration-v6.md for details.")]
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
#endif

    [UnconditionalSuppressMessage("Trimming", "IL2055:MakeGenericType",
        Justification = "The generic type arguments (TC, TK, TV) are derived from typeToConvert which is the actual type being deserialized. The converter types (DictionaryBaseConverter, IDictionaryBaseConverter, DictionaryConverter, ReadOnlyDictionaryConverter) are all defined in this assembly and will be preserved by the trimmer.")]
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