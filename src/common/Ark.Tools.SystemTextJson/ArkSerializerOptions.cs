namespace System.Text.Json;

/// <summary>
/// JsonSerializer with ArkDefaultSettings
/// </summary>
public static class ArkSerializerOptions
{
    public static JsonSerializerOptions JsonOptions
    {
        [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
        [RequiresDynamicCode("Ark default converters create generic converters per type at runtime.")]
        get => _jsonOptions;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The singleton instance is created here but warnings are propagated through the JsonOptions property getter.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "The singleton instance is created here but warnings are propagated through the JsonOptions property getter.")]
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
#if NET9_0_OR_GREATER
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
#endif
    }.ConfigureArkDefaults();
}