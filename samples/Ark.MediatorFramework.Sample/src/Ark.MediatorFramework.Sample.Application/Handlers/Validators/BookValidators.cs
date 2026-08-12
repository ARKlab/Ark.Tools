// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using FluentValidation;

namespace Ark.MediatorFramework.Sample.Application.Handlers.Validators;

/// <summary>Validates book creation requests.</summary>
public sealed class CreateBookRequestValidator : AbstractValidator<Book_CreateRequest.V1>
{
    /// <summary>Initializes a new instance of the <see cref="CreateBookRequestValidator"/> class.</summary>
    public CreateBookRequestValidator()
    {
        RuleFor(request => request.Data.Title)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(request => request.Data.Author)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(request => request.Data.Genre)
            .NotEqual(Ark.Tools.Core.EvolvableEnum<Book.V1.Genre>.NotSet);
        RuleFor(request => request.Data.ISBN)
            .MaximumLength(20);
    }
}

/// <summary>Validates book update requests.</summary>
public sealed class UpdateBookRequestValidator : AbstractValidator<Book_UpdateRequest.V1>
{
    /// <summary>Initializes a new instance of the <see cref="UpdateBookRequestValidator"/> class.</summary>
    public UpdateBookRequestValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Data.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Data.Author).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Data.Genre).NotEqual(Ark.Tools.Core.EvolvableEnum<Book.V1.Genre>.NotSet);
    }
}

/// <summary>Validates book search requests.</summary>
public sealed class SearchBooksQueryValidator : AbstractValidator<Book_SearchQuery.V1>
{
    /// <summary>Initializes a new instance of the <see cref="SearchBooksQueryValidator"/> class.</summary>
    public SearchBooksQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
    }
}

/// <summary>Validates book cover uploads.</summary>
public sealed class UploadBookCoverRequestValidator : AbstractValidator<UploadBookCoverRequest>
{
    /// <summary>Initializes a new instance of the <see cref="UploadBookCoverRequestValidator"/> class.</summary>
    public UploadBookCoverRequestValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Attachment)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(request => request.Attachment.Name).NotEmpty().MaximumLength(255);
                RuleFor(request => request.Attachment.ContentType)
                    .Must(contentType => string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("Book covers must be JPEG or PNG images.");
            });
    }
}

/// <summary>Validates book print-process creation requests.</summary>
public sealed class CreateBookPrintProcessRequestValidator : AbstractValidator<CreateBookPrintProcessRequest>
{
    /// <summary>Initializes a new instance of the <see cref="CreateBookPrintProcessRequestValidator"/> class.</summary>
    public CreateBookPrintProcessRequestValidator()
    {
        RuleFor(request => request.BookId).NotEmpty();
    }
}
