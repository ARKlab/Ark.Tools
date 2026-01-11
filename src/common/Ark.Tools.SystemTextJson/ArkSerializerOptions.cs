using System.Diagnostics.CodeAnalysis;

namespace System.Text.Json;

/// <summary>
/// JsonSerializer with ArkDefaultSettings
/// </summary>
public static class ArkSerializerOptions
{
    public static JsonSerializerOptions JsonOptions
    {
        [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
        get => _jsonOptions;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The singleton instance is created here but warnings are propagated through the JsonOptions property getter.")]
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
}