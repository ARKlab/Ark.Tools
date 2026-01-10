using System.Diagnostics.CodeAnalysis;

namespace System.Text.Json;

/// <summary>
/// JsonSerializer with ArkDefaultSettings
/// </summary>
public static class ArkSerializerOptions
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "This property provides default JSON options using System.Text.Json. Consumers using this property are expected to use RequiresUnreferencedCode or suppress appropriately for their serialization needs.")]
    public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
}