// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.API.Authorization;

using Ark.Tools.Solid;

using NodaTime;

namespace Ark.MediatorFramework.Sample.API;

/// <summary>Represents a review written for a book.</summary>
public sealed record BookReview
{
    /// <summary>Gets the review identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the reviewed book identifier.</summary>
    public required Guid BookId { get; init; }

    /// <summary>Gets the authenticated reviewer identifier.</summary>
    public required string UserId { get; init; }

    /// <summary>Gets the rating from one to five.</summary>
    public required int Rating { get; init; }

    /// <summary>Gets the review text.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the review creation timestamp.</summary>
    public required Instant CreatedAt { get; init; }
}

/// <summary>Creates a review for a book.</summary>
[HttpEndpoint("POST", "/api/v{version}/books/{bookId}/reviews")]
[RebusMessage(OwnerQueue = "ark-mediator-sample")]
[RequireScopePolicy(ApplicationScopes.BookReviewsWrite)]
public sealed record CreateBookReviewRequest :
    IRequest<CreateBookReviewRequest, BookReview>
{
    /// <summary>Gets the reviewed book identifier.</summary>
    [HttpRoute]
    public Guid BookId { get; init; }

    /// <summary>Gets the rating from one to five.</summary>
    public int Rating { get; init; }

    /// <summary>Gets the review text.</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>Lists reviews written for a book.</summary>
[HttpEndpoint("GET", "/api/v{version}/books/{bookId}/reviews")]
[RequireScopePolicy(ApplicationScopes.BookReviewsRead)]
public sealed record ListBookReviewsQuery :
    IQuery<ListBookReviewsQuery, IReadOnlyList<BookReview>>
{
    /// <summary>Gets the reviewed book identifier.</summary>
    [HttpRoute]
    public Guid BookId { get; init; }

    /// <summary>Gets the number of reviews to skip.</summary>
    [HttpQuery]
    public int Skip { get; init; }

    /// <summary>Gets the maximum number of reviews to return.</summary>
    [HttpQuery]
    public int Limit { get; init; } = 25;
}
