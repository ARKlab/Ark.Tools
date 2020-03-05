using Newtonsoft.Json;

namespace Ark.Tools.NewtonsoftJson
{
    // for quick retrocompatibility
    public class ArkDefaultJsonSerializerSettings : ArkJsonSerializerSettings
    {
        public static ArkDefaultJsonSerializerSettings Instance { get; } = new ArkDefaultJsonSerializerSettings();
    }

    public class ArkJsonSerializerSettings : JsonSerializerSettings
    {

        public ArkJsonSerializerSettings()
        {
            this.ConfigureArkDefaults();
        }
    }
}
