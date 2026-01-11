using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.NewtonsoftJson;

// for quick retrocompatibility
public class ArkDefaultJsonSerializerSettings : ArkJsonSerializerSettings
{
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    public ArkDefaultJsonSerializerSettings() : base()
    {
    }

    /// <summary>
    /// Gets a singleton instance of ArkDefaultJsonSerializerSettings.
    /// </summary>
    /// <returns>A shared instance of ArkDefaultJsonSerializerSettings.</returns>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    public static ArkDefaultJsonSerializerSettings GetInstance() => _instance;

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The singleton instance is created here but warnings are propagated through the GetInstance() method.")]
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
    /// <summary>
    /// Gets a singleton JsonSerializer instance with ArkDefaultSettings.
    /// </summary>
    /// <returns>A shared JsonSerializer instance.</returns>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    public static JsonSerializer GetInstance() => _instance;

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The singleton instance is created here but warnings are propagated through the GetInstance() method.")]
    private static readonly JsonSerializer _instance = JsonSerializer.Create(ArkDefaultJsonSerializerSettings.GetInstance());
}