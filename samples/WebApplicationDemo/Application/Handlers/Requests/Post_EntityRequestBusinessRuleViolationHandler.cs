using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Solid;



using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Requests;

public class EntityRequestBusinessRuleViolationHandler : IRequestHandler<Post_EntityRequestBusinessRuleViolation.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Post_EntityRequestBusinessRuleViolation.V1 request)
    {
        return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
    }

    public Task<Entity.V1.Output> ExecuteAsync(Post_EntityRequestBusinessRuleViolation.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problem = new CustomBusinessRuleViolation()
        {
            Balance = 30.0m,
            Accounts = { "/account/12345", "/account/67890" },
            Status = StatusCodes.Status400BadRequest,
            Detail = "DETAILS!!!"
        };

        throw new BusinessRuleViolationException(problem);
    }
}