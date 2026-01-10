using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Ark.Tools.SystemTextJson;


// https://github.com/dotnet/runtime/issues/38812#issuecomment-740648217
public sealed class UniversalInvariantTypeConverterJsonConverter : JsonConverterFactory
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The typeToConvert parameter comes from types that have TypeConverterAttribute, ensuring the TypeConverter is preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067:DynamicallyAccessedMembersMismatch",
        Justification = "The typeToConvert parameter comes from types that have TypeConverterAttribute, ensuring the TypeConverter is preserved.")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var typeConverter = TypeDescriptor.GetConverter(typeToConvert);
        var jsonConverter = (JsonConverter?)Activator.CreateInstance(typeof(TypeConverterJsonConverter<>).MakeGenericType(
                [typeToConvert]),
            [typeConverter, options]);
        return jsonConverter;
    }

    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.GetCustomAttribute<TypeConverterAttribute>() != null;

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via reflection in CreateConverter method")]
    private sealed class TypeConverterJsonConverter<T> : JsonConverter<T>
    {
        private readonly TypeConverter _typeConverter;

        public TypeConverterJsonConverter(TypeConverter tc, JsonSerializerOptions options) =>
            _typeConverter = tc;

        public override bool CanConvert(Type typeToConvert) =>
            _typeConverter.CanConvertFrom(typeof(string)) || _typeConverter.CanConvertTo(typeof(string));

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            if (text == null)
                return default;
            else
                return (T?)_typeConverter.ConvertFromInvariantString(text);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value != null && _typeConverter.ConvertToInvariantString(value) is { } x)
            {
                writer.WriteStringValue(x);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}