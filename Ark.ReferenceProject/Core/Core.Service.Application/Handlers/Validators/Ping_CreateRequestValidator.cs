using Core.Service.API.Requests;
using Core.Service.Common.Dto;
using Core.Service.Common.Enum;

using FluentValidation;


namespace Core.Service.Application.Handlers.Validators
{
    public class Ping_CreateRequestValidator : AbstractValidator<Ping_CreateRequest.V1>
    {
        public Ping_CreateRequestValidator(IValidator<Ping.V1.Create> validator)
        {
            RuleFor(x => x.Data)
                .SetValidator(validator);
        }
    }

    public class Ping_CreateValidator : AbstractValidator<Ping.V1.Create>
    {
        public Ping_CreateValidator()
        {
            RuleFor(x => x.Name)
                .NotNull()
                .NotEmpty()
                .MinimumLength(4)
                .MaximumLength(50)
                ;

            RuleFor(x => x.Type)
                .NotNull()
                .NotEqual(PingType.NotSet)
                ;
        }
    }
}
