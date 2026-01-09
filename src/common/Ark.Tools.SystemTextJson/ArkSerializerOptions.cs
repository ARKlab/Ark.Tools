
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.SystemTextJson(net10.0)', Before:
namespace System.Text.Json
{
    /// <summary>
    /// JsonSerializer with ArkDefaultSettings
    /// </summary>
    public static class ArkSerializerOptions
    {
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    }
=======
namespace System.Text.Json;

/// <summary>
/// JsonSerializer with ArkDefaultSettings
/// </summary>
public static class ArkSerializerOptions
{
    public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
>>>>>>> After
namespace System.Text.Json;

/// <summary>
/// JsonSerializer with ArkDefaultSettings
/// </summary>
public static class ArkSerializerOptions
{
    public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
}