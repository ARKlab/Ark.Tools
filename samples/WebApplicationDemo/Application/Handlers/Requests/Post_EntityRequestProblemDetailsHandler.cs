using Ark.Tools.Solid;

using Hellang.Middleware.ProblemDetails;



using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Requests;

public class Post_EntityRequestProblemDetailsHandler : IRequestHandler<Post_EntityRequestProblemDetails.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Post_EntityRequestProblemDetails.V1 request)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public Task<Entity.V1.Output> ExecuteAsync(Post_EntityRequestProblemDetails.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problem = new OutOfCreditProblemDetails()
        {
            Balance = 30.0m,
            Accounts = { "/account/12345", "/account/67890" },
            Status = StatusCodes.Status400BadRequest,
        };

        throw new ProblemDetailsException(problem);
    }
}
