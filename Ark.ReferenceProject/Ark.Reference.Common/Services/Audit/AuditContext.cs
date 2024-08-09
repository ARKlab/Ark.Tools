using Dapper;

using NLog;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common.Services.Audit
{
    public class AuditContext<TAuditKind>
        where TAuditKind : struct, Enum
    {
        private readonly Logger _logger;
        private readonly IDbConnection _dbConnection;
        private readonly IDbTransaction _dbTransaction;

        private readonly string _schemaAudit = "dbo";
        private readonly string _tableAudit = "Audit";

        public AuditContext(Logger logger, IDbConnection dbConnection, IDbTransaction dbTransaction)
        {
            _logger = logger;
            _dbConnection = dbConnection;
            _dbTransaction = dbTransaction;
        }


        public async Task<(IEnumerable<AuditDto<TAuditKind>> records, int totalCount)> ReadAuditByFilterAsync(
                  AuditQueryDto.V1<TAuditKind> query
                , CancellationToken ctk = default
            )
        {
            _logger.Trace("ReadAuditByFilterAsync called");

            var parameters = new
            {
                AuditIds = (query.AuditIds?.Select(x => x.ToString())),
                query.Users,
                query.FromDateTime,
                query.ToDateTime,
                AuditKinds = (query.AuditKinds?.Select(x => x.ToString())),
                query.Skip,
                query.Limit,
            };
            var cmd = new CommandDefinition($@"
                SELECT
                      [AuditId]
                    , [UserId]
                    , [Kind]
                    , [Info]
                    , [SysStartTime]
                    , [SysEndTime]
                FROM [{_schemaAudit}].[{_tableAudit}]
                WHERE 1=1
                    {(query.AuditIds?.Length > 0 ? "AND [AuditId] IN @AuditIds" : "")}
                    {(query.Users?.Length > 0 ? "AND [UserId] IN @Users" : "")}
                    {(query.AuditKinds?.Length > 0 ? "AND [Kind] IN @AuditKinds" : "")}
                    {(query.FromDateTime != null ? "AND [SysStartTime] >= @FromDateTime" : "")}
                    {(query.ToDateTime != null ? "AND [SysStartTime] <= @ToDateTime" : "")}
                ORDER BY [SysStartTime] DESC
                OFFSET @Skip ROWS FETCH NEXT @Limit ROWS ONLY

                SELECT COUNT(*)
                FROM [{_schemaAudit}].[{_tableAudit}]
                WHERE 1=1
                    {(query.AuditIds?.Length > 0 ? "AND [AuditId] IN @AuditIds" : "")}
                    {(query.Users?.Length > 0 ? "AND [UserId] IN @Users" : "")}
                    {(query.AuditKinds?.Length > 0 ? "AND [Kind] IN @AuditKinds" : "")}
                    {(query.FromDateTime != null ? "AND [SysStartTime] >= @FromDateTime" : "")}
                    {(query.ToDateTime != null ? "AND [SysStartTime] <= @ToDateTime" : "")}
            ", 
            parameters, transaction: _dbTransaction, cancellationToken: ctk);

            using var q = await _dbConnection.QueryMultipleAsync(cmd);

            var retVal = await q.ReadAsync<AuditDto<TAuditKind>>();
            var count = await q.ReadFirstAsync<int>();

            _logger.Trace("ReadAuditById ended");
            return (retVal, count);
        }


        public async Task<IEnumerable<string>> ReadAuditUsersAsync(CancellationToken ctk = default)
        {
            _logger.Trace("ReadAuditUsersAsync called");

            var parameters = new
            {
            };

            var cmd = new CommandDefinition($@"
            SELECT
                DISTINCT [UserId]
            FROM [{_schemaAudit}].[{_tableAudit}]
            ", parameters, transaction: _dbTransaction, cancellationToken: ctk);

            var retVal = await _dbConnection.QueryAsync<string>(cmd).ConfigureAwait(true);

            _logger.Trace("ReadAuditUsersAsync ended");
            return retVal;
        }

        public AuditDto<TAuditKind> CurrentAudit { get; private set; }

        public async ValueTask<AuditDto<TAuditKind>> EnsureAudit(TAuditKind kind, string userId, string infoMessage, CancellationToken ctk = default)
        {
            if (CurrentAudit == null)
            {
                CurrentAudit = new AuditDto<TAuditKind>
                {
                    AuditId = Guid.NewGuid(),
                    UserId = userId,
                    Kind = kind,
                    Info = infoMessage
                };

                await _insertAudit(CurrentAudit, ctk);
            }
            else if (userId != CurrentAudit.UserId)
            {
                throw new InvalidOperationException("Developer exception: turn brain on");
            }

            return CurrentAudit;
        }

        private Task _insertAudit(AuditDto<TAuditKind> dto, CancellationToken ctk = default)
        {
            _logger.Trace("CreateAudit called");

            var parameters = new
            {
                dto.AuditId,
                dto.UserId,
                Kind = dto.Kind.ToString(),
                dto.Info,
            };

            var cmd = new CommandDefinition($@"
            INSERT INTO [{_schemaAudit}].[{_tableAudit}]
            (
                  [AuditId]
                , [UserId]
                , [Kind]
                , [Info]
            )
            VALUES
            (
                  @AuditId
                , @UserId
                , @Kind
                , @Info
            )
            ", parameters, transaction: _dbTransaction, cancellationToken: ctk);

            _logger.Trace("CreateAudit ended");
            return _dbConnection.ExecuteAsync(cmd);
        }
    }
}
