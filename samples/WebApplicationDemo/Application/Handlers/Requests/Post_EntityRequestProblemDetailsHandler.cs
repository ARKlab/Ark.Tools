using Ark.Tools.Solid;

using Hellang.Middleware.ProblemDetails;



using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Requests;

public class Post_EntityRequestProblemDetailsHandler : IRequestHandler<Post_EntityRequestProblemDetails.V1, Entity.V1.Output>
{
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
