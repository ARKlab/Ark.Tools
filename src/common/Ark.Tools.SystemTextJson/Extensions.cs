using Ark.Tools.Nodatime.SystemTextJson;
using Ark.Tools.SystemTextJson;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json;

public static class Extensions
{
    public static JsonSerializerOptions ConfigureArkDefaults(this JsonSerializerOptions @this)
    {
        @this.AllowTrailingCommas = true;
        @this.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        @this.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        @this.PropertyNameCaseInsensitive = true;

        //@this.Converters.Insert(0, new NullableStructSerializerFactory()); // not required anymore in v5
        @this.Converters.Add(new JsonStringEnumMemberConverter()); // from macross
        @this.Converters.Add(new GenericDictionaryWithConvertibleKey());
        @this.Converters.Add(new ValueCollectionJsonConverterFactory());

        @this.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        @this.ConfigureForNodaTimeRanges();

        @this.Converters.Add(new JsonIPAddressConverter());
        @this.Converters.Add(new JsonIPEndPointConverter());

        @this.Converters.Add(new UniversalInvariantTypeConverterJsonConverter()); // as last resort

        return @this;
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo<TObj> for trim-safe serialization.")]
    public static byte[]? SerializeToByte<TObj>(this TObj obj, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (obj == null)
            return null;

        return JsonSerializer.SerializeToUtf8Bytes(obj, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static byte[]? SerializeToByte<TObj>(this TObj obj, JsonTypeInfo<TObj> jsonTypeInfo)
    {
        if (obj == null)
            return null;

        return JsonSerializer.SerializeToUtf8Bytes(obj, jsonTypeInfo);
    }

    public static byte[]? SerializeToByte<TObj>(this TObj obj, JsonSerializerContext context) where TObj : class
    {
        if (obj == null)
            return null;

        return JsonSerializer.SerializeToUtf8Bytes(obj, typeof(TObj), context);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo<TOut> for trim-safe deserialization.")]
    public static TOut? Deserialize<TOut>(this byte[] bytes, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (bytes == null)
            return default;

        var span = new ReadOnlySpan<byte>(bytes);
        if (span.StartsWith(Encoding.UTF8.Preamble)) // UTF8 BOM
            span = span.Slice(Encoding.UTF8.Preamble.Length);

        return JsonSerializer.Deserialize<TOut>(span, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static TOut? Deserialize<TOut>(this byte[] bytes, JsonTypeInfo<TOut> jsonTypeInfo)
    {
        if (bytes == null)
            return default;

        var span = new ReadOnlySpan<byte>(bytes);
        if (span.StartsWith(Encoding.UTF8.Preamble)) // UTF8 BOM
            span = span.Slice(Encoding.UTF8.Preamble.Length);

        return JsonSerializer.Deserialize(span, jsonTypeInfo);
    }

    public static TOut? Deserialize<TOut>(this byte[] bytes, JsonSerializerContext context)
    {
        if (bytes == null)
            return default;

        var span = new ReadOnlySpan<byte>(bytes);
        if (span.StartsWith(Encoding.UTF8.Preamble)) // UTF8 BOM
            span = span.Slice(Encoding.UTF8.Preamble.Length);

        return (TOut?)JsonSerializer.Deserialize(span, typeof(TOut), context);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo<TObj> for trim-safe serialization.")]
    public static string? Serialize<TObj>(this TObj obj, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (obj == null)
            return null;

        return JsonSerializer.Serialize(obj, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static string? Serialize<TObj>(this TObj obj, JsonTypeInfo<TObj> jsonTypeInfo)
    {
        if (obj == null)
            return null;

        return JsonSerializer.Serialize(obj, jsonTypeInfo);
    }

    public static string? Serialize<TObj>(this TObj obj, JsonSerializerContext context) where TObj : class
    {
        if (obj == null)
            return null;

        return JsonSerializer.Serialize(obj, typeof(TObj), context);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo<TOut> for trim-safe deserialization.")]
    public static TOut? Deserialize<TOut>(this string s, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (s == null)
            return default;

        return JsonSerializer.Deserialize<TOut>(s, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static TOut? Deserialize<TOut>(this string s, JsonTypeInfo<TOut> jsonTypeInfo)
    {
        if (s == null)
            return default;

        return JsonSerializer.Deserialize(s, jsonTypeInfo);
    }

    public static TOut? Deserialize<TOut>(this string s, JsonSerializerContext context)
    {
        if (s == null)
            return default;

        return (TOut?)JsonSerializer.Deserialize(s, typeof(TOut), context);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use JsonSerializer.Deserialize with JsonTypeInfo<T> for trim-safe deserialization.")]
    public static T? ToObject<T>(this JsonElement element, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        return element.GetRawText().Deserialize<T>(jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static T? ToObject<T>(this JsonElement element, JsonTypeInfo<T> jsonTypeInfo)
    {
        return JsonSerializer.Deserialize(element, jsonTypeInfo);
    }

    public static T? ToObject<T>(this JsonElement element, JsonSerializerContext context)
    {
        return (T?)JsonSerializer.Deserialize(element, typeof(T), context);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use JsonSerializer.Deserialize with JsonTypeInfo<T> for trim-safe deserialization.")]
    public static T? ToObject<T>(this JsonDocument document, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        return document.RootElement.GetRawText().Deserialize<T>(jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static T? ToObject<T>(this JsonDocument document, JsonTypeInfo<T> jsonTypeInfo)
    {
        return JsonSerializer.Deserialize(document, jsonTypeInfo);
    }

    public static T? ToObject<T>(this JsonDocument document, JsonSerializerContext context)
    {
        return (T?)JsonSerializer.Deserialize(document, typeof(T), context);
    }


    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use JsonSerializer.Deserialize with JsonTypeInfo for trim-safe deserialization.")]
    public static object? Deserialize(this byte[] bytes, Type type, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (bytes == null)
            return default;

        return JsonSerializer.Deserialize(bytes, type, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static object? Deserialize(this byte[] bytes, Type type, JsonSerializerContext context)
    {
        if (bytes == null)
            return default;

        return JsonSerializer.Deserialize(bytes, type, context);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use JsonSerializer.Deserialize with JsonTypeInfo for trim-safe deserialization.")]
    public static object? Deserialize(this string jsonString, Type type, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (string.IsNullOrEmpty(jsonString))
            return default;

        return JsonSerializer.Deserialize(jsonString, type, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static object? Deserialize(this string jsonString, Type type, JsonSerializerContext context)
    {
        if (string.IsNullOrEmpty(jsonString))
            return default;

        return JsonSerializer.Deserialize(jsonString, type, context);
    }


    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use JsonSerializer.Deserialize with JsonTypeInfo<TOut> for trim-safe deserialization.")]
    public static TOut? DeserializeFromFile<TOut>(this string jsonStringFilePath, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (string.IsNullOrEmpty(jsonStringFilePath))
            return default;

        using var fs = new FileStream(jsonStringFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize<TOut>(fs, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static TOut? DeserializeFromFile<TOut>(this string jsonStringFilePath, JsonTypeInfo<TOut> jsonTypeInfo)
    {
        if (string.IsNullOrEmpty(jsonStringFilePath))
            return default;

        using var fs = new FileStream(jsonStringFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize(fs, jsonTypeInfo);
    }

    public static TOut? DeserializeFromFile<TOut>(this string jsonStringFilePath, JsonSerializerContext context)
    {
        if (string.IsNullOrEmpty(jsonStringFilePath))
            return default;

        using var fs = new FileStream(jsonStringFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (TOut?)JsonSerializer.Deserialize(fs, typeof(TOut), context);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use JsonSerializer.Serialize with JsonTypeInfo for trim-safe serialization.")]
    public static string? Serialize(this object obj, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (obj == null)
            return null;

        return JsonSerializer.Serialize(obj, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
    }

    public static string? Serialize(this object obj, Type type, JsonSerializerContext context)
    {
        if (obj == null)
            return null;

        return JsonSerializer.Serialize(obj, type, context);
    }
}