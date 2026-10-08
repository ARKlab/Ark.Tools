// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core.BusinessRuleViolation;

namespace Ark.MediatorFramework.Sample.Core.Application.Exceptions;

/// <summary>Indicates that a review identifier already belongs to a review of another book.</summary>
public sealed class BookReviewIdConflictViolation : BusinessRuleViolation
{
    /// <summary>Initializes a new instance of the <see cref="BookReviewIdConflictViolation"/> class.</summary>
    /// <param name="reviewId">The review identifier that is already in use.</param>
    public BookReviewIdConflictViolation(Guid reviewId)
        : base("The review identifier is already in use.")
    {
        ReviewId = reviewId;
        Detail = $"Review '{reviewId:D}' already belongs to a review of another book.";
    }

    /// <summary>Gets the review identifier that is already in use.</summary>
    public Guid ReviewId { get; }
}
