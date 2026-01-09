using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;

using FluentValidation;

namespace Ark.Reference.Core.Application.Handlers.Validators
{
    /// <summary>
    /// Validator for Book creation requests
    /// </summary>
    public class Book_CreateRequestValidator : AbstractValidator<Book_CreateRequest.V1>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Book_CreateRequestValidator"/> class
        /// </summary>
        /// <param name="validator">The validator for the Book create data</param>
        public Book_CreateRequestValidator(IValidator<Book.V1.Create> validator)
        {
            RuleFor(x => x.Data)
                .NotNull()
                .SetValidator(validator!);
        }
    }

    /// <summary>
    /// Validator for Book creation data
    /// </summary>
    public class Book_CreateValidator : AbstractValidator<Book.V1.Create>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Book_CreateValidator"/> class
        /// </summary>
        public Book_CreateValidator()
        {
            RuleFor(x => x.Title)
                .NotNull()
                .NotEmpty()
                .MinimumLength(1)
                .MaximumLength(200)
                ;

            RuleFor(x => x.Author)
                .NotNull()
                .NotEmpty()
                .MinimumLength(1)
                .MaximumLength(100)
                ;

            RuleFor(x => x.Genre)
                .NotNull()
                .NotEqual(BookGenre.NotSet)
                ;

            RuleFor(x => x.ISBN)
                .MaximumLength(20)
                .When(x => x.ISBN != null)
                ;
        }
    }
}