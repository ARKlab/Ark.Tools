// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Vogen;

namespace Ark.MediatorFramework.Sample.Core.API;

/// <summary>Identifies a book review.</summary>
[ValueObject<Guid>]
public readonly partial struct BookReviewId
{
    /// <summary>Creates a new random identifier.</summary>
    /// <returns>A new identifier.</returns>
    public static BookReviewId New() => From(Guid.NewGuid());
}
