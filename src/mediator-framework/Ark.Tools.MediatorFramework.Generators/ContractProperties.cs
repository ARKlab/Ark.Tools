// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>
/// The contract property walk shared by the MediatorFramework generators. Linked as source into each
/// generator assembly, so it must stay <see langword="internal"/>.
/// </summary>
internal static class ContractProperties
{
    /// <summary>
    /// Gets the properties declared by the type and then by each of its base types, without filtering.
    /// Overridden and hidden properties are returned once per declaring type.
    /// </summary>
    /// <param name="type">The contract type.</param>
    /// <returns>The declared properties, most-derived type first.</returns>
    public static IEnumerable<IPropertySymbol> All(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                yield return property;
        }
    }
}
