using Ark.Reference.Core.API.Queries;

using FluentValidation;


namespace Ark.Reference.Core.Application.Handlers.Validators
{
    /// <summary>
    /// Validator for the Ping GetByName query (for testing/demonstration)
    /// </summary>
    public class Ping_DeepCheckQueryValidator : AbstractValidator<Ping_GetByNameQuery.V1>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Ping_DeepCheckQueryValidator"/> class
        /// </summary>
        public Ping_DeepCheckQueryValidator()
        {
            RuleFor(x => x.Name)
                .NotEqual("FAIL")
                .MinimumLength(4)
                ;
        }
    }
}