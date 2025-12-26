using Ark.Reference.Core.API.Messages;
using System.Text.Json.Serialization;

namespace Ark.Reference.Core.API.JsonContext;

/// <summary>
/// JSON serialization source generation context for Rebus messages
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Ping_ProcessMessage.V1))]
public sealed partial class RebusMessagesJsonSerializerContext : JsonSerializerContext
{
}
