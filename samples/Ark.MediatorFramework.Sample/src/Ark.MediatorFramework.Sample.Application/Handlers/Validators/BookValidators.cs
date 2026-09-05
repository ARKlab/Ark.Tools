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
        RuleFor(static request => request.Data)
            .SetValidator(new BookCreateValidator());
    }
}

/// <summary>Validates book creation data.</summary>
public sealed class BookCreateValidator : AbstractValidator<Book.V1.Create>
{
    /// <summary>Initializes a new instance of the <see cref="BookCreateValidator"/> class.</summary>
    public BookCreateValidator()
    {
        RuleFor(static book => book.Title)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(static book => book.Author)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(static book => book.Genre)
            .NotEqual(Ark.Tools.Core.EvolvableEnum<Book.V1.Genre>.NotSet);
        RuleFor(static book => book.ISBN)
            .MaximumLength(20);
    }
}

/// <summary>Validates bulk book creation requests.</summary>
public sealed class BulkCreateBookRequestValidator : AbstractValidator<Book_BulkCreateRequest.V1>
{
    /// <summary>Initializes a new instance of the <see cref="BulkCreateBookRequestValidator"/> class.</summary>
    public BulkCreateBookRequestValidator()
    {
        RuleFor(static request => request.Data).NotEmpty();
        RuleForEach(static request => request.Data).SetValidator(new BookCreateValidator());
    }
}

/// <summary>Validates book update requests.</summary>
public sealed class UpdateBookRequestValidator : AbstractValidator<Book_UpdateRequest.V1>
{
    /// <summary>Initializes a new instance of the <see cref="UpdateBookRequestValidator"/> class.</summary>
    public UpdateBookRequestValidator()
    {
        RuleFor(static request => request.Id).NotEmpty();
        RuleFor(static request => request.Data.Title).NotEmpty().MaximumLength(200);
        RuleFor(static request => request.Data.Author).NotEmpty().MaximumLength(100);
        RuleFor(static request => request.Data.Genre).NotEqual(Ark.Tools.Core.EvolvableEnum<Book.V1.Genre>.NotSet);
    }
}

/// <summary>Validates book search requests.</summary>
public sealed class SearchBooksQueryValidator : AbstractValidator<Book_SearchQuery.V1>
{
    /// <summary>Initializes a new instance of the <see cref="SearchBooksQueryValidator"/> class.</summary>
    public SearchBooksQueryValidator()
    {
        RuleFor(static query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(static query => query.Limit).InclusiveBetween(1, 100);

        When(static query => query.Sort is not null, () =>
        {
            RuleForEach(static query => query.Sort)
                .Must(_isValidSort)
                .WithMessage("Invalid book sort '{PropertyValue}'.");
        });
    }

    private static readonly HashSet<string> _sortProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Book.V1.Output.Id),
        nameof(Book.V1.Output.Title),
        nameof(Book.V1.Output.Author),
        nameof(Book.V1.Output.Genre),
        nameof(Book.V1.Output.ISBN),
        nameof(Book.V1.Output.Description),
    };

    private static bool _isValidSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return true;

        var parts = sort.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2)
            return false;

        if (!_sortProperties.Contains(parts[0]))
            return false;

        return parts.Length == 1
            || parts[1].Equals("ASC", StringComparison.OrdinalIgnoreCase)
            || parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Validates book cover uploads.</summary>
public sealed class UploadBookCoverRequestValidator : AbstractValidator<UploadBookCoverRequest.V1>
{
    /// <summary>Initializes a new instance of the <see cref="UploadBookCoverRequestValidator"/> class.</summary>
    public UploadBookCoverRequestValidator()
    {
        RuleFor(static request => request.Id).NotEmpty();
        RuleFor(static request => request.Attachment)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(static request => request.Attachment.Name).NotEmpty().MaximumLength(255);
                RuleFor(static request => request.Attachment.ContentType)
                    .Must(static contentType => string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("Book covers must be JPEG or PNG images.");
            });
    }
}

/// <summary>Validates book print-process creation requests.</summary>
public sealed class CreateBookPrintProcessRequestValidator : AbstractValidator<CreateBookPrintProcessRequest.V1>
{
    /// <summary>Initializes a new instance of the <see cref="CreateBookPrintProcessRequestValidator"/> class.</summary>
    public CreateBookPrintProcessRequestValidator()
    {
        RuleFor(static request => request.BookId).NotEmpty();
    }
}

/// <summary>Validates book review creation requests.</summary>
public sealed class CreateBookReviewRequestValidator : AbstractValidator<CreateBookReviewRequest.V1>
{
    /// <summary>Initializes a new instance of the <see cref="CreateBookReviewRequestValidator"/> class.</summary>
    public CreateBookReviewRequestValidator()
    {
        RuleFor(static request => request.BookId).NotEmpty();
        RuleFor(static request => request.Rating).InclusiveBetween(1, 5);
        RuleFor(static request => request.Text).NotEmpty().MaximumLength(2000);
    }
}

/// <summary>Validates book review list queries.</summary>
public sealed class ListBookReviewsQueryValidator : AbstractValidator<ListBookReviewsQuery.V1>
{
    /// <summary>Initializes a new instance of the <see cref="ListBookReviewsQueryValidator"/> class.</summary>
    public ListBookReviewsQueryValidator()
    {
        RuleFor(static query => query.BookId).NotEmpty();
        RuleFor(static query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(static query => query.Limit).InclusiveBetween(1, 100);
    }
}

/// <summary>Validates reading activity requests.</summary>
public sealed class RecordReadingActivityRequestValidator : AbstractValidator<RecordReadingActivityRequest.V1>
{
    /// <summary>Initializes a new instance of the <see cref="RecordReadingActivityRequestValidator"/> class.</summary>
    public RecordReadingActivityRequestValidator()
    {
        RuleFor(static request => request.BookId).NotEmpty();
        RuleFor(static request => request.Kind).NotEqual(Ark.Tools.Core.EvolvableEnum<ReadingActivityKind>.NotSet);
        RuleFor(static request => request.Progress).InclusiveBetween(0, 100);
        RuleFor(static request => request)
            .Must(static request => request.Kind != ReadingActivityKind.Started || request.Progress == 0)
            .WithMessage("Started activity must have zero progress.");
        RuleFor(static request => request)
            .Must(static request => request.Kind != ReadingActivityKind.Finished || request.Progress == 100)
            .WithMessage("Finished activity must have complete progress.");
    }
}

/// <summary>Validates reading activity queries.</summary>
public sealed class GetReadingActivityQueryValidator : AbstractValidator<GetReadingActivityQuery.V1>
{
    /// <summary>Initializes a new instance of the <see cref="GetReadingActivityQueryValidator"/> class.</summary>
    public GetReadingActivityQueryValidator()
    {
        RuleFor(static query => query.BookId).NotEmpty();
        RuleFor(static query => query.Limit).InclusiveBetween(1, 100);
    }
}
