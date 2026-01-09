using Newtonsoft.Json;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.NewtonsoftJson(net10.0)', Before:
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

    /// <summary>
    /// JsonSerializer with ArkDefaultSettings
    /// </summary>
    public static class ArkJsonSerializer
    {
        public static JsonSerializer Instance { get; } = JsonSerializer.Create(ArkDefaultJsonSerializerSettings.Instance);
    }


=======
namespace Ark.Tools.NewtonsoftJson;

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

/// <summary>
/// JsonSerializer with ArkDefaultSettings
/// </summary>
public static class ArkJsonSerializer
{
    public static JsonSerializer Instance { get; } = JsonSerializer.Create(ArkDefaultJsonSerializerSettings.Instance);
>>>>>>> After
    namespace Ark.Tools.NewtonsoftJson;

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

    /// <summary>
    /// JsonSerializer with ArkDefaultSettings
    /// </summary>
    public static class ArkJsonSerializer
    {
        public static JsonSerializer Instance { get; } = JsonSerializer.Create(ArkDefaultJsonSerializerSettings.Instance);
    }