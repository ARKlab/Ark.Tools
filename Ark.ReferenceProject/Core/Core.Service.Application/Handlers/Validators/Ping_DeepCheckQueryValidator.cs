using Core.Service.API.Queries;

using FluentValidation;


namespace Core.Service.Application.Handlers.Validators
{
    public class Ping_DeepCheckQueryValidator : AbstractValidator<Ping_GetByNameQuery.V1>
    {
        public Ping_DeepCheckQueryValidator()
        {
            RuleFor(x => x.Name)
                .NotEqual("FAIL")
                .MinimumLength(4)
                ;
        }
    }
}
