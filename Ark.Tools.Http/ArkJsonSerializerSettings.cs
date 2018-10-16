using Ark.Tools.Nodatime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.JsonNet;

namespace Ark.Tools.Http
{
    public class ArkJsonSerializerSettings : JsonSerializerSettings
    {
        public ArkJsonSerializerSettings()
        {
            this.TypeNameHandling = TypeNameHandling.None;
            this.ObjectCreationHandling = ObjectCreationHandling.Replace;
            this.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            this.ConfigureForNodaTimeRanges();
            this.Converters.Add(new StringEnumConverter());
            this.ContractResolver = new CamelCasePropertyNamesContractResolver();
            this.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            this.ObjectCreationHandling = ObjectCreationHandling.Replace;
        }
    }
}
