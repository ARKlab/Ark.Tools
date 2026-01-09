using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;

using FluentValidation;


namespace Ark.Reference.Core.Application.Handlers.Validators
{
    /// <summary>
    /// Validator for Ping creation requests
    /// </summary>
    public class Ping_CreateRequestValidator : AbstractValidator<Ping_CreateRequest.V1>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Ping_CreateRequestValidator"/> class
        /// </summary>
        /// <param name="validator">The validator for the Ping create data</param>
        public Ping_CreateRequestValidator(IValidator<Ping.V1.Create> validator)
        {
            RuleFor(x => x.Data)
                .NotNull()
                .SetValidator(validator!);
        }
    }

    /// <summary>
    /// Validator for Ping creation data
    /// </summary>
    public class Ping_CreateValidator : AbstractValidator<Ping.V1.Create>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Ping_CreateValidator"/> class
        /// </summary>
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