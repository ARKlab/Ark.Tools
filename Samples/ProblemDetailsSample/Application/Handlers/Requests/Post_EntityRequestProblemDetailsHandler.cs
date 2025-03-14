﻿using Ark.Tools.Solid;

using EnsureThat;

using Hellang.Middleware.ProblemDetails;

using Microsoft.AspNetCore.Http;

using ProblemDetailsSample.Api.Requests;
using ProblemDetailsSample.Common.Dto;
using ProblemDetailsSample.Models;

using System.Threading;
using System.Threading.Tasks;

namespace ProblemDetailsSample.Api.Queries
{
    public class Post_EntityRequestProblemDetailsHandler : IRequestHandler<Post_EntityRequestProblemDetails.V1, Entity.V1.Output>
    {
        public Entity.V1.Output Execute(Post_EntityRequestProblemDetails.V1 request)
        {
            return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public Task<Entity.V1.Output> ExecuteAsync(Post_EntityRequestProblemDetails.V1 request, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(request, nameof(request));

            var problem = new OutOfCreditProblemDetails()
            {
                Detail = "Your current balance is 30, but that costs 50.",
                Instance = "/account/12345/msgs/abc",
                Balance = 30.0m,
                Accounts = { "/account/12345", "/account/67890" },
                Status = StatusCodes.Status400BadRequest,
            };

            throw new ProblemDetailsException(problem);
        }
    }
}
