// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Vogen;

namespace Ark.MediatorFramework.Sample.Core.API;

/// <summary>The rating of a book review, from one to five.</summary>
[ValueObject<int>]
public readonly partial struct ReviewRating
{
}
