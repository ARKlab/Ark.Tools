using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Common.Services.Audit.Dto;
using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using Ark.Tools.Solid;

using Microsoft.AspNetCore.Mvc;

using NodaTime;


namespace Ark.Reference.Core.WebInterface.Controllers;

[Route("audit")]
[ApiController]
public class AuditController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IRequestProcessor _requestProcessor;

    public AuditController(IQueryProcessor queryProcessor, IRequestProcessor requestProcessor)
    {
        _queryProcessor = queryProcessor;
        _requestProcessor = requestProcessor;
    }

    /// <summary>
    /// Retrieves the audits by filters
    /// </summary>
    /// <param name="auditIds">Filter by audit IDs</param>
    /// <param name="users">Filter by user names</param>
    /// <param name="fromDateTime">Filter audits from this date/time</param>
    /// <param name="toDateTime">Filter audits to this date/time</param>
    /// <param name="auditKinds">Filter by audit kinds</param>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="ctk">Cancellation token</param>
    /// <returns>A paged result of audit records</returns>
    /// <response code="200">The audit array</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditDto<AuditKind>>), 200)]
    public async Task<IActionResult> GetAuditQuery(
          [FromQuery] Guid[] auditIds
        , [FromQuery] string[] users
        , [FromQuery] LocalDateTime? fromDateTime
        , [FromQuery] LocalDateTime? toDateTime
        , [FromQuery] AuditKind[] auditKinds
        , [FromQuery] int skip
        , [FromQuery] int limit
        , CancellationToken ctk = default)
    {
        var query = new Audit_GetQuery.V1()
        {
            AuditIds = auditIds,
            Users = users,
            FromDateTime = fromDateTime,
            ToDateTime = toDateTime,
            AuditKinds = auditKinds,
            Skip = skip,
            Limit = limit
        };

        var res = await _queryProcessor.ExecuteAsync(query, ctk: ctk).ConfigureAwait(false);

        return this.Ok(res);
    }

    /// <summary>
    /// Retrieves the changes for a specific audit record
    /// </summary>
    /// <param name="auditId">The audit ID to retrieve changes for</param>
    /// <param name="ctk">Cancellation token</param>
    /// <returns>The audit changes showing previous and current values</returns>
    /// <response code="200">The audit changes</response>
    [HttpGet("{auditID:guid}/changes")]
    [ProducesResponseType(typeof(AuditRecordReturn.V1<IAuditEntity>), 200)]
    public async Task<IActionResult> GetAuditChangesQuery([FromRoute] Guid auditId, CancellationToken ctk = default)
    {
        var res = await _queryProcessor.ExecuteAsync(new Audit_GetChangesQuery.V1()
        {
            AuditId = auditId
        }, ctk: ctk).ConfigureAwait(false);

        return this.Ok(res);
    }

    /// <summary>
    /// Retrieves list of audit users
    /// </summary>
    /// <param name="ctk">Cancellation token</param>
    /// <returns>A list of unique user names that have audit records</returns>
    /// <response code="200">List of user names</response>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public async Task<IActionResult> GetAuditUsersQuery(CancellationToken ctk = default)
    {
        var query = new Audit_GetUsersQuery.V1();

        var res = await _queryProcessor.ExecuteAsync(query, ctk: ctk).ConfigureAwait(false);

        return this.Ok(res);
    }
}