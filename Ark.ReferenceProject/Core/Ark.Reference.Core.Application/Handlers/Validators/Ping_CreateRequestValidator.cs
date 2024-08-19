using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;

using FluentValidation;


namespace Ark.Reference.Core.Application.Handlers.Validators
{
    public class Ping_CreateRequestValidator : AbstractValidator<Ping_CreateRequest.V1>
    {
        public Ping_CreateRequestValidator(IValidator<Ping.V1.Create> validator)
        {
            RuleFor(x => x.Data)
                .NotNull()
                .SetValidator(validator!);
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
