using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Ark.Tools.SystemTextJson;

/// <summary>
/// JsonConverterFactory that provides TypeConverter-based serialization for types decorated with TypeConverterAttribute.
/// </summary>
/// <remarks>
/// <para>
/// This converter bridges a fundamental gap in System.Text.Json: it does not natively support the TypeConverter pattern,
/// unlike Newtonsoft.Json. See <see href="https://github.com/dotnet/runtime/issues/38812">GitHub issue #38812</see>.
/// </para>
/// <para>
/// <strong>Status as of .NET 8</strong>: The issue is closed as "Won't Fix" - System.Text.Json will NOT support 
/// TypeConverter attributes by design. This is a permanent difference from Newtonsoft.Json.
/// </para>
/// <para>
/// This converter enables types with TypeConverterAttribute to be serialized/deserialized as strings,
/// maintaining compatibility with Newtonsoft.Json behavior and allowing gradual migration.
/// </para>
/// </remarks>
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