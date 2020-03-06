﻿using Ark.Tools.Nodatime;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.JsonNet;

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
            @this.ContractResolver = new CamelCasePropertyNamesContractResolver();            

            return @this;
        }
    }
}
