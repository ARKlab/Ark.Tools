// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Linq;

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
    /// Each name is returned once, as name lookup from outside the type sees it: the most-derived public
    /// declaration, or the most-derived declaration when none of that name is public. So an overridden or
    /// hidden property comes from the most-derived type, but a non-public <see langword="new"/> member
    /// never hides a public base one.
    /// </summary>
    /// <param name="type">The contract type.</param>
    /// <returns>The declared properties, most-derived type first.</returns>
    public static IEnumerable<IPropertySymbol> All(INamedTypeSymbol type)
    {
        var declared = new List<IPropertySymbol>();
        for (var current = type; current is not null; current = current.BaseType)
            declared.AddRange(current.GetMembers().OfType<IPropertySymbol>());

        var publicNames = new HashSet<string>(
            declared.Where(static property => property.DeclaredAccessibility == Accessibility.Public).Select(static property => property.Name),
            StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return declared.Where(property =>
            (property.DeclaredAccessibility == Accessibility.Public || !publicNames.Contains(property.Name))
            && seen.Add(property.Name));
    }
}
