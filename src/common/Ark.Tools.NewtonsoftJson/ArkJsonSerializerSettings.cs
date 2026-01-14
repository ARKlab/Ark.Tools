using Newtonsoft.Json;

namespace Ark.Tools.NewtonsoftJson;

// for quick retrocompatibility
public class ArkDefaultJsonSerializerSettings : ArkJsonSerializerSettings
{
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    public ArkDefaultJsonSerializerSettings() : base()
    {
    }

    public static ArkDefaultJsonSerializerSettings Instance
    {
        [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
        get => _instance;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The singleton instance is created here but warnings are propagated through the Instance property getter.")]
    private static readonly ArkDefaultJsonSerializerSettings _instance = new ArkDefaultJsonSerializerSettings();
}

public class ArkJsonSerializerSettings : JsonSerializerSettings
{
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
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
    public static JsonSerializer Instance
    {
        [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
        get => _instance;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The singleton instance is created here but warnings are propagated through the Instance property getter.")]
    private static readonly JsonSerializer _instance = JsonSerializer.Create(ArkDefaultJsonSerializerSettings.Instance);
}