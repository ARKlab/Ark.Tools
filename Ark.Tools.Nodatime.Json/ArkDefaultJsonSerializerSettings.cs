using Newtonsoft.Json;

namespace Ark.Tools.Nodatime.Json
{
    public class ArkDefaultJsonSerializerSettings : JsonSerializerSettings
    {
        public readonly static ArkDefaultJsonSerializerSettings Instance = new ArkDefaultJsonSerializerSettings();

        public ArkDefaultJsonSerializerSettings()
        {
            this.ConfigureForArkDefault();
        }
    }
}
