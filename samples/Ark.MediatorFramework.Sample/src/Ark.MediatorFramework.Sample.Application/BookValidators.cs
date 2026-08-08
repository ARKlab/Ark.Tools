// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using FluentValidation;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Validates book creation requests.</summary>
public sealed class CreateBookRequestValidator : AbstractValidator<CreateBookRequest>
{
    /// <summary>Initializes a new instance of the <see cref="CreateBookRequestValidator"/> class.</summary>
    public CreateBookRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(request => request.Author)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(request => request.Genre)
            .NotEqual(Ark.Tools.Core.EvolvableEnum<BookGenre>.NotSet);
        RuleFor(request => request.ISBN)
            .MaximumLength(20);
    }
}

/// <summary>Validates book update requests.</summary>
public sealed class UpdateBookRequestValidator : AbstractValidator<UpdateBookRequest>
{
    /// <summary>Initializes a new instance of the <see cref="UpdateBookRequestValidator"/> class.</summary>
    public UpdateBookRequestValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Author).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Genre).NotEqual(Ark.Tools.Core.EvolvableEnum<BookGenre>.NotSet);
        RuleFor(request => request.ISBN).MaximumLength(20);
    }
}

/// <summary>Validates book search requests.</summary>
public sealed class SearchBooksQueryValidator : AbstractValidator<SearchBooksQuery>
{
    /// <summary>Initializes a new instance of the <see cref="SearchBooksQueryValidator"/> class.</summary>
    public SearchBooksQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
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
