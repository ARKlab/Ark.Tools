using Ark.Tools.Core;
using Ark.Tools.Solid;
using EnsureThat;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Http;
using NodaTime;
using ProblemDetailsSample.Api.Requests;
using ProblemDetailsSample.Common.Dto;
using ProblemDetailsSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProblemDetailsSample.Api.Queries
{
    public class Post_EntityRequestHandler : IRequestHandler<Post_EntityRequest.V1, Entity.V1.Output>
    {
        public Entity.V1.Output Execute(Post_EntityRequest.V1 request)
        {
            return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<Entity.V1.Output> ExecuteAsync(Post_EntityRequest.V1 request, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(request, nameof(request));

            var entity = new Entity.V1.Output()
            {
                EntityId = "StringLongerThan10"
            };

            return await Task.FromResult(entity);
        }
    }
}
