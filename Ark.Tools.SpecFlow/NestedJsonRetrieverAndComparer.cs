using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using System;
using System.Collections.Generic;
using Ark.Tools.Nodatime;
using TechTalk.SpecFlow.Assist;
using Ark.Tools.Http;
using Ark.Tools.NewtonsoftJson;

namespace Ark.Tools.SpecFlow
{
    public class NestedJsonRetrieverAndComparer<TType> : IValueRetriever, IValueComparer
    {
        private static readonly JsonSerializerSettings _settings;

        static NestedJsonRetrieverAndComparer()
        {
            var s = new ArkJsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };

            s.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            s.ConfigureForNodaTimeRanges();
            s.Converters.Add(new StringEnumConverter());

            _settings = s;
        }

        public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            if (string.IsNullOrWhiteSpace(keyValuePair.Value)) return null;

            return JsonConvert.DeserializeObject(keyValuePair.Value, propertyType, _settings);
        }

        public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return propertyType == typeof(TType);
        }

        public bool CanCompare(object actualValue)
        {
            return actualValue?.GetType() == typeof(TType);
        }

        public bool Compare(string expectedValue, object actualValue)
        {
            if (actualValue == null ^ string.IsNullOrWhiteSpace(expectedValue)) return false;
            if (string.IsNullOrWhiteSpace(expectedValue) && actualValue == null) return true;
            if (actualValue == null) return false;

            return JToken.DeepEquals(JToken.Parse(expectedValue), JToken.FromObject(actualValue, JsonSerializer.Create(_settings)));
        }
    }
}
