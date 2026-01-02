namespace System.Text.Json
{
    /// <summary>
    /// JsonSerializer with ArkDefaultSettings
    /// </summary>
    public static class ArkSerializerOptions
    {
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    }
}