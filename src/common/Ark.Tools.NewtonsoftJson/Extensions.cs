using Ark.Tools.NewtonsoftJson;
using Ark.Tools.Nodatime;

using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.JsonNet;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.NewtonsoftJson(net10.0)', Before:
namespace Newtonsoft.Json
{
    public static class Extensions
    {
        public static JsonSerializerSettings ConfigureArkDefaults(this JsonSerializerSettings @this)
        {
            @this.TypeNameHandling = TypeNameHandling.None;
            @this.ObjectCreationHandling = ObjectCreationHandling.Replace;
            @this.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            @this.ConfigureForNodaTimeRanges();
            @this.Converters.Add(new StringEnumConverter());
            @this.Converters.Add(new ValueCollectionConverter());
            @this.ContractResolver = new CamelCasePropertyNamesContractResolver();

            return @this;
        }
=======
namespace Newtonsoft.Json;

public static class Extensions
{
    public static JsonSerializerSettings ConfigureArkDefaults(this JsonSerializerSettings @this)
    {
        @this.TypeNameHandling = TypeNameHandling.None;
        @this.ObjectCreationHandling = ObjectCreationHandling.Replace;
        @this.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        @this.ConfigureForNodaTimeRanges();
        @this.Converters.Add(new StringEnumConverter());
        @this.Converters.Add(new ValueCollectionConverter());
        @this.ContractResolver = new CamelCasePropertyNamesContractResolver();

        return @this;
>>>>>>> After


namespace Newtonsoft.Json;

public static class Extensions
{
    public static JsonSerializerSettings ConfigureArkDefaults(this JsonSerializerSettings @this)
    {
        @this.TypeNameHandling = TypeNameHandling.None;
        @this.ObjectCreationHandling = ObjectCreationHandling.Replace;
        @this.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        @this.ConfigureForNodaTimeRanges();
        @this.Converters.Add(new StringEnumConverter());
        @this.Converters.Add(new ValueCollectionConverter());
        @this.ContractResolver = new CamelCasePropertyNamesContractResolver();

        return @this;
    }
}