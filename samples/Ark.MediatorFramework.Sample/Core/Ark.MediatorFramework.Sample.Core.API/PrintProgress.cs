// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Vogen;

namespace Ark.MediatorFramework.Sample.Core.API;

/// <summary>The completed fraction of a book print process, from zero to one.</summary>
[ValueObject<double>]
[Instance("Zero", 0.0, "A print process that has not started.")]
public readonly partial struct PrintProgress
{
}
