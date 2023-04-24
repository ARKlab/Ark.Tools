using Ark.Tools.SystemTextJson;
using Ark.Tools.Nodatime.SystemTextJson;

using NodaTime;

using System.Text.Json.Serialization;
using NodaTime.Serialization.SystemTextJson;

namespace System.Text.Json
{
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

        public static byte[]? SerializeToByte<TObj>(this TObj obj, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            if (obj == null)
                return null;

            return JsonSerializer.SerializeToUtf8Bytes(obj, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
        }

        public static TOut? DeserializeFromByte<TOut>(this byte[] bytes, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            if (bytes == null)
                return default;

            var readOnlySpan = new ReadOnlySpan<byte>(bytes);

            return JsonSerializer.Deserialize<TOut>(readOnlySpan, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
        }

        public static string? Serialize<TObj>(this TObj obj, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            if (obj == null)
                return null;

            return JsonSerializer.Serialize(obj, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
        }

        public static TOut? Deserialize<TOut>(this string s, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            if (s == null)
                return default;

            return JsonSerializer.Deserialize<TOut>(s, jsonSerializerOptions ?? ArkSerializerOptions.JsonOptions);
        }

        public static T? ToObject<T>(this JsonElement element, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            return element.GetRawText().Deserialize<T>(jsonSerializerOptions);
        }

        public static T? ToObject<T>(this JsonDocument document, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            return document.RootElement.GetRawText().Deserialize<T>(jsonSerializerOptions);
        }
    }
}
