using Ark.Reference.Common;
using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Sql.SqlServer;

using Dapper;

using NodaTime;

using System.Globalization;

using static Dapper.SqlMapper;

namespace Ark.Reference.Core.Application.DAL;

public partial class CoreDataContext_Sql
{
    private const string _schemaPing = "dbo";
    private const string _tablePing = "Ping";

    public async Task<Ping.V1.Output?> ReadPingByIdAsync(int id, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "ReadPingByIdAsync called");

        var (data, _) = await ReadPingByFiltersAsync(new PingSearchQueryDto.V1()
        {
            Id = [id],
            Limit = 1
        }, ctk).ConfigureAwait(false);

        _logger.Trace(CultureInfo.InvariantCulture, "ReadPingByIdAsync ended");

        return data.SingleOrDefault();
    }

    public async Task<(IEnumerable<Ping.V1.Output> data, int count)> ReadPingByFiltersAsync(PingSearchQueryDto.V1 query, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "ReadPingByFiltersAsync called");

        var sortFields = query.Sort.CompileSorts(new Dictionary<string, string>
(StringComparer.Ordinal)
        {
            {"id", "E.[Id]" },
        }, "E.[Id] DESC");

        var parameters = new
        {
            @Id = query.Id,
            @Name = query.Name,
            @Type = query.Type?.Select(x => x.ToString()),
            @Skip = query.Skip,
            @Limit = query.Limit
        };

        var cmdText = $@"
                SELECT 
                      E.[Id]
                    , E.[Name]
                    , E.[Type]
                    , E.[Code]

                FROM [{_schemaPing}].[{_tablePing}] E

                WHERE 1 = 1
                  {(query.Id?.Length > 0 ? "AND E.[Id]   IN @Id" : "")}
                  {(query.Name?.Length > 0 ? "AND E.[Name] IN @Name" : "")}
                  {(query.Type?.Length > 0 ? "AND E.[Type] IN @Type" : "")}
            "
        .AsSqlServerPagedQuery(sortFields);

        var cmd = new CommandDefinition(cmdText, parameters, transaction: Transaction, cancellationToken: ctk);

        var (data, count) = await Connection.ReadPagedAsync<PingView>(cmd).ConfigureAwait(false);

        var d = data.Select(s => s.ToOutput());

        _logger.Trace(CultureInfo.InvariantCulture, "ReadPingByFiltersAsync ended");

        return (d, count);
    }

    public async Task<int> InsertPingAsync(Ping.V1.Output entity, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "InsertPingAsync called");

        var parameters = new
        {
            @Name = entity.Name,
            @Type = entity.Type.ToString(),
            @Code = entity.Code,
            @AuditId = CurrentAudit?.AuditId,
        };

        var cmdText = $@"
                INSERT INTO [{_schemaPing}].[{_tablePing}]
                (
                      [Name]
                    , [Type]
                    , [Code]
                    , [AuditId]
                )
                VALUES
                (
                      @Name
                    , @Type
                    , @Code
                    , @AuditId
                );

                SELECT
                  SCOPE_IDENTITY();
            ";

        var cmd = new CommandDefinition(
            cmdText,
            parameters,
            transaction: Transaction,
            cancellationToken: ctk
        );

        var id = await Connection.QuerySingleAsync<int>(cmd).ConfigureAwait(false);

        _logger.Trace(CultureInfo.InvariantCulture, "InsertPingAsync ended");

        return id;
    }

    public async Task PutPingAsync(Ping.V1.Output entity, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "PutPingAsync called");

        var parameters = new
        {
            @Id = entity.Id,
            @Name = entity.Name,
            @Type = entity.Type.ToString(),
            @Code = entity.Code,
            @AuditId = CurrentAudit?.AuditId,
        };

        var query = @$"
                UPDATE  [{_schemaPing}].[{_tablePing}]

                SET
                     [Name] = @Name
                    ,[Type] = @Type
                    ,[Code] = @Code
                    ,[AuditId] = @AuditId

                WHERE 1=1
                AND  [Id] = @Id
            ";

        var cmd = new CommandDefinition(
            query,
            parameters,
            transaction: Transaction
        );

        await Connection.ExecuteAsync(cmd).ConfigureAwait(false);

        _logger.Trace(CultureInfo.InvariantCulture, "PutPingAsync ended");

        return;
    }

    public async Task PatchPingAsync(Ping.V1.Output entity, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "PatchPingAsync called");

        var parameters = new
        {
            @Id = entity.Id,
            @Name = entity.Name,
            @Type = entity.Type?.ToString(),
            @Code = entity.Code,
            @AuditId = CurrentAudit?.AuditId,
        };

        var updateValues = new List<string>();

        if (entity.Name != null)
            updateValues.Add($"[Name] = @Name");

        if (entity.Type != null)
            updateValues.Add($"[Type] = @Type");

        var query = @$"
                UPDATE  [{_schemaPing}].[{_tablePing}]

                SET
                    {string.Join(", ", updateValues)}
                    ,[Code] = @Code
                    ,[AuditId] = @AuditId 

                WHERE 1=1
                AND  [Id] = @Id
            ";

        var cmd = new CommandDefinition(
            query,
            parameters,
            transaction: Transaction
        );

        await Connection.ExecuteAsync(cmd).ConfigureAwait(false);

        _logger.Trace(CultureInfo.InvariantCulture, "PatchPingAsync ended");

        return;
    }

    public async Task DeletePingAsync(int id, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "DeletePingAsync called");

        var parameters = new
        {
            @Id = id
        };

        var cmdText = $@"
                DELETE FROM [{_schemaPing}].[{_tablePing}]
                WHERE 1 = 1 
                    AND [Id] = @Id
            ";

        var cmd = new CommandDefinition(
            cmdText,
            parameters,
            transaction: Transaction,
            cancellationToken: ctk
        );

        await Connection.ExecuteAsync(cmd).ConfigureAwait(false);

        _logger.Trace(CultureInfo.InvariantCulture, "DeletePingAsync ended");

        return;
    }


    public async Task<(AuditedEntityDto<Ping.V1.Output>? pre, AuditedEntityDto<Ping.V1.Output>? cur)> ReadPingAuditAsync(Guid auditId, CancellationToken ctk = default)
    {
        var param = new
        {
            @AuditId = auditId,
        };

        var cmd = new CommandDefinition(
            $@"
                    SELECT 
                          F.[Id]
                        , F.[Name]
                        , F.[Type]
                        , F.[Code]

                        , F.[AuditId]

                        , F.[SysStartTime]
                        , F.[SysEndTime]

                    FROM 
                        [{_schemaPing}].[{_tablePing}] FOR SYSTEM_TIME ALL F

                    INNER JOIN 
                        [{_schemaPing}].[{_tablePing}] FOR SYSTEM_TIME ALL R
                        ON 1=1
                            AND F.[Id] = R.[Id]

                    WHERE 1=1
                            AND R.[AuditId] = @AuditId
                ",
            param, transaction: Transaction, cancellationToken: ctk
         );

        var data = await Connection.QueryAsync<PingView>(cmd).ConfigureAwait(false);

        var resTable = data
            .Select(s => new AuditedEntityDto<Ping.V1.Output>()
            {
                Entity = s.ToOutput(),
                SysStartTime = s.SysStartTime!.Value,
                SysEndTime = s.SysEndTime!.Value
            })
            .ToList();

        var cur = resTable.FirstOrDefault(w => w.Entity!.AuditId == auditId);
        var pre = resTable.FirstOrDefault(w => w.Entity!.AuditId != auditId);

        return (pre, cur);
    }



    #region Private view
    private sealed record PingView
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Code { get; set; }


        public Guid AuditId { get; set; }
        public Instant? SysStartTime { get; set; }
        public Instant? SysEndTime { get; set; }

        public Ping.V1.Output ToOutput()
        {
            return new Ping.V1.Output
            {
                Id = Id,
                Name = Name,
                Type = string.IsNullOrEmpty(Type) ? null : Enum.Parse<PingType>(Type),
                Code = Code,

                AuditId = AuditId,
            };
        }
    }
    #endregion
}