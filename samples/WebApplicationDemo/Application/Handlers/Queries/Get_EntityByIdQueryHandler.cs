using Ark.Tools.Solid;

using NLog;

using NodaTime;

using System.Globalization;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Queries;

public class Get_EntityByIdQueryHandler : IQueryHandler<Get_EntityByIdQuery.V1, Entity.V1.Output?>
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public Entity.V1.Output? Execute(Get_EntityByIdQuery.V1 query)
    {
        return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
    }

    public async Task<Entity.V1.Output?> ExecuteAsync(Get_EntityByIdQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.EntityId == "null")
            return null;

        var entity = new Entity.V1.Output()
        {
            EntityId = query.EntityId,
            Date = NodaTime.SystemClock.Instance.GetCurrentInstant().InUtc().Date,
            EntityResult = EntityResult.Success1 | EntityResult.Success2,
            EntityTest = EntityTest.Prova1,
            Strings = new Ark.Tools.Core.ValueCollection<string>(System.StringComparer.Ordinal) { "antani" },
            Ts = new Dictionary<LocalDate, double?>
            {
                { NodaTime.SystemClock.Instance.GetCurrentInstant().InUtc().Date, null }
            }
        };

        _logger.Info(CultureInfo.InvariantCulture, "Entity {EntityId} found!", entity.EntityId);

        return await Task.FromResult(entity).ConfigureAwait(false);
    }
}