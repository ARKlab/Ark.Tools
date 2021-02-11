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
            @this.Converters.Add(new JsonStringEnumConverter());
            @this.Converters.Add(new GenericDictionaryWithConvertibleKey());

            @this.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            @this.ConfigureForNodaTimeRanges();

            @this.Converters.Add(new UniversalInvariantTypeConverterJsonConverter());

            return @this;
        }
    }
}
