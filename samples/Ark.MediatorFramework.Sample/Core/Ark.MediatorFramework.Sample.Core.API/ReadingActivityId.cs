// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Vogen;

namespace Ark.MediatorFramework.Sample.Core.API;

/// <summary>Identifies a reading activity event.</summary>
[ValueObject<Guid>]
public readonly partial struct ReadingActivityId
{
    /// <summary>Creates a new random identifier.</summary>
    /// <returns>A new identifier.</returns>
    public static ReadingActivityId New() => From(Guid.NewGuid());
}
