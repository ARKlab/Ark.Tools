using Ark.Tools.Solid;

using Hellang.Middleware.ProblemDetails;


using ProblemDetailsSample.Api.Requests;
using ProblemDetailsSample.Common.Dto;
using ProblemDetailsSample.Models;


namespace ProblemDetailsSample.Api.Queries;

public class Post_EntityRequestProblemDetailsHandler : IRequestHandler<Post_EntityRequestProblemDetails.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Post_EntityRequestProblemDetails.V1 request)
    {
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable VSTHRD002 // Sync wrapper for legacy API
        return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public Task<Entity.V1.Output> ExecuteAsync(Post_EntityRequestProblemDetails.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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
