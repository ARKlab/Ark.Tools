using FluentValidation;

using ProblemDetailsSample.Api.Queries;

namespace ProblemDetailsSample.Application.Handlers.Vaildators;

public class Get_EntityByIdQueryValidator : AbstractValidator<Get_EntityByIdQuery.V1>
{
    public Get_EntityByIdQueryValidator()
    {
        RuleFor(x => x.EntityId)
            .NotEmpty()
            .MaximumLength(10)
            ;
    }
}