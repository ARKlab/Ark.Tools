using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Queries
{
    /// <summary>
    /// Handler for testing Ping retrieval by name (demonstration/test handler)
    /// </summary>
    internal sealed class Ping_TestByNameQueryHandler : IQueryHandler<Ping_GetByNameQuery.V1, Ping.V1.Output>
    {
        /// <inheritdoc/>
        public Ping.V1.Output Execute(Ping_GetByNameQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async Task<Ping.V1.Output> ExecuteAsync(Ping_GetByNameQuery.V1 query, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            return new Ping.V1.Output()
            {
                Name = query.Name,
                Code = $"PING_CODE_{query.Name}"
            };
        }
    }
}