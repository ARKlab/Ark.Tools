using Ark.Reference.Core.API.Requests;

using FluentValidation;

namespace Ark.Reference.Core.Application.Handlers.Validators;

/// <summary>
/// Validates bulk book creation requests.
/// </summary>
public sealed class Book_BulkCreateRequestValidator : AbstractValidator<Book_BulkCreateRequest.V1>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Book_BulkCreateRequestValidator"/> class.
    /// </summary>
    public Book_BulkCreateRequestValidator()
    {
        RuleFor(static x => x.Data)
            .NotEmpty();

        RuleForEach(static x => x.Data)
            .SetValidator(new Book_CreateValidator());
    }
}
