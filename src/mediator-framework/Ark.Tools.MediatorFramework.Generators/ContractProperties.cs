// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
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
    /// An overridden or hidden property is returned once, from the most-derived type that declares it.
    /// </summary>
    /// <param name="type">The contract type.</param>
    /// <returns>The declared properties, most-derived type first.</returns>
    public static IEnumerable<IPropertySymbol> All(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (seen.Add(property.Name))
                    yield return property;
            }
        }
    }
}
