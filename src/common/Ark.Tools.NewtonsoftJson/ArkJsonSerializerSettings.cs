using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.NewtonsoftJson;

// for quick retrocompatibility
public class ArkDefaultJsonSerializerSettings : ArkJsonSerializerSettings
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This class wraps Newtonsoft.Json functionality which requires reflection. Consumers using this class are expected to use RequiresUnreferencedCode or suppress appropriately.")]
    public ArkDefaultJsonSerializerSettings() : base()
    {
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This class wraps Newtonsoft.Json functionality which requires reflection. Consumers using this property are expected to use RequiresUnreferencedCode or suppress appropriately.")]
    public static ArkDefaultJsonSerializerSettings Instance { get; } = new ArkDefaultJsonSerializerSettings();
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
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This class wraps Newtonsoft.Json functionality which requires reflection. Consumers using this property are expected to use RequiresUnreferencedCode or suppress appropriately.")]
    public static JsonSerializer Instance { get; } = JsonSerializer.Create(ArkDefaultJsonSerializerSettings.Instance);
}