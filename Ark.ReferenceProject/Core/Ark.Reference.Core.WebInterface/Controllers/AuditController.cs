using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.Core;
using Ark.Tools.Solid;
using Ark.Reference.Common.Services.Audit.Dto;
using Ark.Reference.Common.Services.Audit;
using Microsoft.AspNetCore.Mvc;

using NodaTime;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.API.Queries;

namespace Ark.Reference.Core.WebInterface.Controllers
{
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
        /// <remarks></remarks>
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
            try
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

                var res = await _queryProcessor.ExecuteAsync(query, ctk: ctk);

                return this.Ok(res);
            }
            catch (Exception ex)
            {
                _ = ex.Message;
                throw;
            }
        }

        /// <summary>
        /// Retrieves the audits by filters
        /// </summary>
        /// <remarks></remarks>
        /// <response code="200">The audit array</response>
        [HttpGet("{auditID:guid}/changes")]
        [ProducesResponseType(typeof(AuditRecordReturn.V1<IAuditEntity>), 200)]
        public async Task<IActionResult> GetAuditChangesQuery([FromRoute] Guid auditId, CancellationToken ctk = default)
        {
            var res = await _queryProcessor.ExecuteAsync(new Audit_GetChangesQuery.V1()
            {
                AuditId = auditId
            }, ctk: ctk);

            return this.Ok(res);
        }

        /// <summary>
        /// Retrieves list of audit users
        /// </summary>
        /// <remarks></remarks>
        /// <response code="200"></response>
        [HttpGet("users")]
        [ProducesResponseType(typeof(IEnumerable<string>), 200)]
        public async Task<IActionResult> GetAuditUsersQuery(CancellationToken ctk = default)
        {
            var query = new Audit_GetUsersQuery.V1();

            var res = await _queryProcessor.ExecuteAsync(query, ctk: ctk);

            return this.Ok(res);
        }
    }
}
