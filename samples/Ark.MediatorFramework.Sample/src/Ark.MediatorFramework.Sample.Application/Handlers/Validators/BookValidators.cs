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

        When(query => query.Sort is not null, () =>
        {
            RuleForEach(query => query.Sort)
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

/// <summary>Validates book review creation requests.</summary>
public sealed class CreateBookReviewRequestValidator : AbstractValidator<CreateBookReviewRequest>
{
    /// <summary>Initializes a new instance of the <see cref="CreateBookReviewRequestValidator"/> class.</summary>
    public CreateBookReviewRequestValidator()
    {
        RuleFor(request => request.BookId).NotEmpty();
        RuleFor(request => request.Rating).InclusiveBetween(1, 5);
        RuleFor(request => request.Text).NotEmpty().MaximumLength(2000);
    }
}

/// <summary>Validates book review list queries.</summary>
public sealed class ListBookReviewsQueryValidator : AbstractValidator<ListBookReviewsQuery>
{
    /// <summary>Initializes a new instance of the <see cref="ListBookReviewsQueryValidator"/> class.</summary>
    public ListBookReviewsQueryValidator()
    {
        RuleFor(query => query.BookId).NotEmpty();
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
    }
}

/// <summary>Validates reading activity requests.</summary>
public sealed class RecordReadingActivityRequestValidator : AbstractValidator<RecordReadingActivityRequest>
{
    /// <summary>Initializes a new instance of the <see cref="RecordReadingActivityRequestValidator"/> class.</summary>
    public RecordReadingActivityRequestValidator()
    {
        RuleFor(request => request.BookId).NotEmpty();
        RuleFor(request => request.Kind).NotEqual(Ark.Tools.Core.EvolvableEnum<ReadingActivityKind>.NotSet);
        RuleFor(request => request.Progress).InclusiveBetween(0, 100);
        RuleFor(request => request)
            .Must(request => request.Kind != ReadingActivityKind.Started || request.Progress == 0)
            .WithMessage("Started activity must have zero progress.");
        RuleFor(request => request)
            .Must(request => request.Kind != ReadingActivityKind.Finished || request.Progress == 100)
            .WithMessage("Finished activity must have complete progress.");
    }
}

/// <summary>Validates reading activity queries.</summary>
public sealed class GetReadingActivityQueryValidator : AbstractValidator<GetReadingActivityQuery>
{
    /// <summary>Initializes a new instance of the <see cref="GetReadingActivityQueryValidator"/> class.</summary>
    public GetReadingActivityQueryValidator()
    {
        RuleFor(query => query.BookId).NotEmpty();
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
    }
}
