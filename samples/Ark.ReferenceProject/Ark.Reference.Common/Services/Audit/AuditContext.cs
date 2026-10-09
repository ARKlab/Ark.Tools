using Dapper;
using Ark.Tools.Compliance;

using NLog;

using System.Data;

namespace Ark.Reference.Common.Services.Audit;

public class AuditContext<TAuditKind>
    where TAuditKind : struct, Enum
{
    private readonly Logger _logger;
    private readonly IDbConnection _dbConnection;
    private readonly IDbTransaction _dbTransaction;

    private readonly string _schemaAudit = "dbo";
    private readonly string _tableAudit = "Audit";
    private AuditDto<TAuditKind>? _currentAudit;

    public AuditContext(Logger logger, IDbConnection dbConnection, IDbTransaction dbTransaction)
    {
        _logger = logger;
        _dbConnection = dbConnection;
        _dbTransaction = dbTransaction;
    }
    public AuditDto<TAuditKind> CurrentAudit => _currentAudit ?? throw new InvalidOperationException("CurrentAudit is null. Call EnsureAudit(...) before accessing CurrentAudit");

    public async Task<(IEnumerable<AuditDto<TAuditKind>> records, int totalCount)> ReadAuditByFilterAsync(
              AuditQueryDto.V1<TAuditKind> query
            , CancellationToken ctk = default
        )
    {
        _logger.Trace(CultureInfo.InvariantCulture, "ReadAuditByFilterAsync called");

        var parameters = new
        {
            AuditIds = (query.AuditIds?.Select(static x => x.ToString())),
            query.Users,
            query.FromDateTime,
            query.ToDateTime,
            AuditKinds = (query.AuditKinds?.Select(static x => x.ToString())),
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
                ORDER BY [SysStartTime] DESC, [AuditId] DESC
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
        await using var q = await _dbConnection.QueryMultipleAsync(cmd).ConfigureAwait(false);

        var retVal = await q.ReadAsync<AuditDto<TAuditKind>>().ConfigureAwait(false);
        var count = await q.ReadFirstAsync<int>().ConfigureAwait(false);

        _logger.Trace(CultureInfo.InvariantCulture, "ReadAuditById ended");
        return (retVal, count);
    }


    public async Task<IEnumerable<string>> ReadAuditUsersAsync(CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "ReadAuditUsersAsync called");

        var parameters = new
        {
        };

        var cmd = new CommandDefinition($@"
            SELECT
                DISTINCT [UserId]
            FROM [{_schemaAudit}].[{_tableAudit}]
            ", parameters, transaction: _dbTransaction, cancellationToken: ctk);

        var retVal = await _dbConnection.QueryAsync<string>(cmd).ConfigureAwait(true);

        _logger.Trace(CultureInfo.InvariantCulture, "ReadAuditUsersAsync ended");
        return retVal;
    }


    public async ValueTask<AuditDto<TAuditKind>> EnsureAudit(TAuditKind kind, [Pseudonymous] string? userId, string? infoMessage, CancellationToken ctk = default)
    {
        if (_currentAudit == null)
        {
            var audit = new AuditDto<TAuditKind>
            {
                UserId = userId,
                Kind = kind,
                Info = infoMessage
            };

            audit.AuditId = await _insertAudit(audit, ctk).ConfigureAwait(false);
            _currentAudit = audit;
        }
        else if (userId != CurrentAudit.UserId)
        {
            throw new InvalidOperationException("Developer exception: turn brain on");
        }

        return CurrentAudit;
    }

    private async Task<Guid> _insertAudit(AuditDto<TAuditKind> dto, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "CreateAudit called");

        var parameters = new
        {
            dto.UserId,
            Kind = dto.Kind.ToString(),
            dto.Info,
        };

        var cmd = new CommandDefinition($@"
            INSERT INTO [{_schemaAudit}].[{_tableAudit}]
            (
                  [UserId]
                , [Kind]
                , [Info]
            )
            -- AuditId comes from the NEWSEQUENTIALID() default so it is monotonic and breaks SysStartTime ties
            -- (consecutive transactions can share the same begin time)
            OUTPUT INSERTED.[AuditId]
            VALUES
            (
                  @UserId
                , @Kind
                , @Info
            )
            ", parameters, transaction: _dbTransaction, cancellationToken: ctk);

        var auditId = await _dbConnection.QuerySingleAsync<Guid>(cmd).ConfigureAwait(false);

        _logger.Trace(CultureInfo.InvariantCulture, "CreateAudit ended");
        return auditId;
    }
}