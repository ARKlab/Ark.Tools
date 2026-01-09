using FluentValidation;

using ProblemDetailsSample.Api.Requests;

namespace ProblemDetailsSample.Application.Handlers.Vaildators;

public class Post_EntityRequestValidator : AbstractValidator<Post_EntityRequest.V1>
{
    public Post_EntityRequestValidator()
    {
        RuleFor(x => x.EntityId)
            .NotEmpty()
            .MaximumLength(10)
            ;

        RuleFor(x => x.EntityId)
            .NotEmpty()
            .MaximumLength(20)
            ;

        RuleFor(x => x.EntityId)
            .NotEqual("ErrorCode")
            .WithErrorCode("NOT_EQUAL_ERRORCODE")
            .WithState(x => new
            {
                Foo = "bar"
            });
    }
}