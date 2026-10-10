// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Vogen;

namespace Ark.Tools.Core.Interceptors.Tests;

/// <summary>A Vogen struct value object over a <see cref="Guid"/>.</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct InterceptedOrderId;

/// <summary>A Vogen class value object over a <see cref="string"/>.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
public sealed partial class InterceptedOrderCode;

/// <summary>A Vogen struct value object declared with the non-generic attribute.</summary>
[ValueObject(typeof(int), comparison: ComparisonGeneration.Omit)]
public readonly partial struct InterceptedOrderLine;

/// <summary>A flat entity carrying required and optional Vogen value object members.</summary>
public sealed class InterceptedValueObjectEntity
{
    /// <summary>Gets or sets the identifier.</summary>
    public InterceptedOrderId Id { get; set; }

    /// <summary>Gets or sets the optional parent identifier.</summary>
    public InterceptedOrderId? ParentId { get; set; }

    /// <summary>Gets or sets the optional code.</summary>
    public InterceptedOrderCode? Code { get; set; }

    /// <summary>Gets or sets the line number.</summary>
    public InterceptedOrderLine Line { get; set; }
}
