using Ark.Tools.SystemTextJson;
using System.Text.Json.Serialization;

namespace System.Text.Json
{
    public static class Extensions
    {
        public static JsonSerializerOptions ConfigureArkDefaults(this JsonSerializerOptions @this)
        {
            //NodeTimeConverter.Register();

            @this.AllowTrailingCommas = true;
            @this.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            @this.Converters.Insert(0, new NullableStructSerializerFactory());
            @this.Converters.Add(new JsonStringEnumConverter());
            @this.Converters.Add(new GenericDictionaryWithConvertibleKey());

            //@this.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            //@this.ConfigureForNodaTimeRanges();

            return @this;
        }
    }
}
