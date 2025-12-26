using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Common.Services.Audit.Dto;
using Ark.Reference.Core.API.Messages;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Core;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Reference.Core.WebInterface.JsonContext;

/// <summary>
/// JSON serialization source generation context for Core API.
/// Includes all root-level types serialized by the application from handler definitions (Requests, Queries, Messages).
/// TypeInfoPropertyName is required to avoid naming collisions when multiple types have the same simple name
/// (e.g., Ping.V1.Output and Book.V1.Output both have "Output" as the type name).
/// Configured with Ark default settings via constructor.
/// </summary>
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Ping.V1.Output), TypeInfoPropertyName = "PingV1Output")]
[JsonSerializable(typeof(Book.V1.Output), TypeInfoPropertyName = "BookV1Output")]
[JsonSerializable(typeof(IEnumerable<Ping.V1.Output>), TypeInfoPropertyName = "IEnumerablePingV1Output")]
[JsonSerializable(typeof(IEnumerable<Book.V1.Output>), TypeInfoPropertyName = "IEnumerableBookV1Output")]
[JsonSerializable(typeof(PagedResult<Ping.V1.Output>), TypeInfoPropertyName = "PagedResultPingV1Output")]
[JsonSerializable(typeof(PagedResult<Book.V1.Output>), TypeInfoPropertyName = "PagedResultBookV1Output")]
[JsonSerializable(typeof(Ping_ProcessMessage.V1), TypeInfoPropertyName = "PingProcessMessageV1")]
[JsonSerializable(typeof(IEnumerable<string>), TypeInfoPropertyName = "IEnumerableString")]
[JsonSerializable(typeof(IEnumerable<AuditDto<Common.Enum.AuditKind>>), TypeInfoPropertyName = "IEnumerableAuditDto")]
[JsonSerializable(typeof(PagedResult<AuditDto<Common.Enum.AuditKind>>), TypeInfoPropertyName = "PagedResultAuditDto")]
[JsonSerializable(typeof(IAuditRecordReturn<IAuditEntity>), TypeInfoPropertyName = "AuditRecordReturn")]
public sealed partial class CoreApiJsonSerializerContext : JsonSerializerContext
{
}
