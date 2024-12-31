using Ark.Tools.Solid;

using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Common.Dto;

using EnsureThat;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Queries
{
    internal sealed class Ping_TestByNameQueryHandler : IQueryHandler<Ping_GetByNameQuery.V1, Ping.V1.Output>
    {

        public Ping_TestByNameQueryHandler()
        { 

        }

        public Ping.V1.Output Execute(Ping_GetByNameQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        public async Task<Ping.V1.Output> ExecuteAsync(Ping_GetByNameQuery.V1 query, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(query, nameof(query));

            return new Ping.V1.Output()
            {
                Name = query.Name,
                Code = $"PING_CODE_{query.Name}"
            };
        }
    }
}
