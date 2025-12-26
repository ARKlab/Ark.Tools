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
/// Only root-level handler response types are registered - nested types are handled automatically by the source generator.
/// Configured with Ark default settings via constructor using TypeInfoResolver pattern.
/// </summary>
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Ping.V1.Output), TypeInfoPropertyName = "PingV1Output")]
[JsonSerializable(typeof(PagedResult<Ping.V1.Output>), TypeInfoPropertyName = "PagedResultPingV1Output")]
[JsonSerializable(typeof(Book.V1.Output), TypeInfoPropertyName = "BookV1Output")]
[JsonSerializable(typeof(PagedResult<Book.V1.Output>), TypeInfoPropertyName = "PagedResultBookV1Output")]
[JsonSerializable(typeof(Ping_ProcessMessage.V1), TypeInfoPropertyName = "PingProcessMessageV1")]
[JsonSerializable(typeof(IAuditRecordReturn<IAuditEntity>), TypeInfoPropertyName = "AuditRecordReturn")]
[JsonSerializable(typeof(IEnumerable<string>), TypeInfoPropertyName = "IEnumerableString")]
[JsonSerializable(typeof(PagedResult<AuditDto<Common.Enum.AuditKind>>), TypeInfoPropertyName = "PagedResultAuditDto")]
[JsonSerializable(typeof(bool))]
public sealed partial class CoreApiJsonSerializerContext : JsonSerializerContext
{
}
