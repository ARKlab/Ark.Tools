using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Solid;


using ProblemDetailsSample.Api.Requests;
using ProblemDetailsSample.Common.Dto;
using ProblemDetailsSample.Models;


namespace ProblemDetailsSample.Api.Queries;

public class EntityRequestBusinessRuleViolationHandler : IRequestHandler<Post_EntityRequestBusinessRuleViolation.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Post_EntityRequestBusinessRuleViolation.V1 request)
    {
#pragma warning disable VSTHRD002 // Sync wrapper for legacy API
        return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    public Task<Entity.V1.Output> ExecuteAsync(Post_EntityRequestBusinessRuleViolation.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problem = new CustomBusinessRuleViolation()
        {
            Balance = 30.0m,
            Accounts = { "/account/12345", "/account/67890" },
            Status = StatusCodes.Status400BadRequest,
        };

        throw new BusinessRuleViolationException(problem);
    }
}