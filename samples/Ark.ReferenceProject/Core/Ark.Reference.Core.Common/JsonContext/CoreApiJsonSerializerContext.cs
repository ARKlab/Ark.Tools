using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ark.Reference.Core.Common.JsonContext;

/// <summary>
/// JSON serialization source generation context for Core API
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Ping.V1.Create), TypeInfoPropertyName = "PingV1Create")]
[JsonSerializable(typeof(Ping.V1.Update), TypeInfoPropertyName = "PingV1Update")]
[JsonSerializable(typeof(Ping.V1.Output), TypeInfoPropertyName = "PingV1Output")]
[JsonSerializable(typeof(PagedResult<Ping.V1.Output>), TypeInfoPropertyName = "PagedResultPingV1Output")]
[JsonSerializable(typeof(IEnumerable<Ping.V1.Output>), TypeInfoPropertyName = "IEnumerablePingV1Output")]
[JsonSerializable(typeof(PingType))]
[JsonSerializable(typeof(Book.V1.Create), TypeInfoPropertyName = "BookV1Create")]
[JsonSerializable(typeof(Book.V1.Update), TypeInfoPropertyName = "BookV1Update")]
[JsonSerializable(typeof(Book.V1.Output), TypeInfoPropertyName = "BookV1Output")]
[JsonSerializable(typeof(PagedResult<Book.V1.Output>), TypeInfoPropertyName = "PagedResultBookV1Output")]
[JsonSerializable(typeof(IEnumerable<Book.V1.Output>), TypeInfoPropertyName = "IEnumerableBookV1Output")]
[JsonSerializable(typeof(BookGenre))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
public sealed partial class CoreApiJsonSerializerContext : JsonSerializerContext
{
}
