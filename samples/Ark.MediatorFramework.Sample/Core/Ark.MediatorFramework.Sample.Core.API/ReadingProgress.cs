// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Vogen;

namespace Ark.MediatorFramework.Sample.Core.API;

/// <summary>The percentage of a book read, from zero to one hundred.</summary>
[ValueObject<int>]
public readonly partial struct ReadingProgress
{
}
