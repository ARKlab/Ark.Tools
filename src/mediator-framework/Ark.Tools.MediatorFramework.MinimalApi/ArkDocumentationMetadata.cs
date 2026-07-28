// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Documentation extracted from a generated mediator contract.</summary>
public sealed class ArkDocumentationMetadata
{
    /// <summary>Initializes a new instance of the <see cref="ArkDocumentationMetadata"/> class.</summary>
    /// <param name="summary">The contract summary.</param>
    /// <param name="remarks">The contract remarks.</param>
    /// <param name="propertyDescriptions">Property names and descriptions.</param>
    public ArkDocumentationMetadata(
        string? summary,
        string? remarks,
        IReadOnlyDictionary<string, string> propertyDescriptions)
    {
        Summary = summary;
        Remarks = remarks;
        PropertyDescriptions = propertyDescriptions;
    }

    /// <summary>Gets the contract summary.</summary>
    public string? Summary { get; }

    /// <summary>Gets the contract remarks.</summary>
    public string? Remarks { get; }

    /// <summary>Gets descriptions keyed by CLR property name.</summary>
    public IReadOnlyDictionary<string, string> PropertyDescriptions { get; }
}
