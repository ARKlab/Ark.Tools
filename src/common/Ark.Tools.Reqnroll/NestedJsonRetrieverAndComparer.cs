using Ark.Tools.NewtonsoftJson;
using Ark.Tools.Nodatime;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using NodaTime;
using NodaTime.Serialization.JsonNet;

using Reqnroll.Assist;

using System.Diagnostics.CodeAnalysis;


namespace Ark.Tools.Reqnroll;

public class NestedJsonRetrieverAndComparer<TType> : IValueRetriever, IValueComparer
{
    private static readonly JsonSerializerSettings _settings;

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This class is used by Reqnroll for test data binding. The TType generic parameter ensures the type is preserved by the test framework.")]
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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This method is used by Reqnroll for test data binding. The propertyType comes from test DTOs that are preserved by the test framework.")]
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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This method is used by Reqnroll for test comparison. The actualValue type is preserved by the test framework.")]
    public bool Compare(string expectedValue, object actualValue)
    {
        if (actualValue == null ^ string.IsNullOrWhiteSpace(expectedValue)) return false;
        if (string.IsNullOrWhiteSpace(expectedValue) && actualValue == null) return true;
        if (actualValue == null) return false;

        return JToken.DeepEquals(JToken.Parse(expectedValue), JToken.FromObject(actualValue, JsonSerializer.Create(_settings)));
    }
}