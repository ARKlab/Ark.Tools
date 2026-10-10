// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>
/// Recognizes Vogen value objects (<c>[Vogen.ValueObject&lt;T&gt;]</c> or <c>[Vogen.ValueObject(typeof(T))]</c>) by
/// attribute name, so the generators take no Vogen dependency. Generators cannot see each other's output, so the
/// members Vogen generates (<c>Value</c>, <c>From</c>, <c>TryParse</c>) are inferred from the attribute.
/// </summary>
internal static class ValueObjectSymbols
{
    /// <summary>Gets the fully-qualified primitive wrapped by a Vogen value object.</summary>
    /// <param name="type">The candidate type.</param>
    /// <returns>The primitive's fully-qualified name, or <see langword="null"/> when the type is not a value object.</returns>
    public static string? GetPrimitive(ITypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass
                || attributeClass.ContainingNamespace?.ToDisplayString() != "Vogen")
            {
                continue;
            }

            if (attributeClass.MetadataName == "ValueObjectAttribute`1")
                return attributeClass.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (attributeClass.MetadataName == "ValueObjectAttribute")
            {
                // Vogen wraps an int when [ValueObject] names no type.
                return attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is ITypeSymbol primitive
                    ? primitive.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : "int";
            }
        }

        return null;
    }

    /// <summary>Gets whether the type is a Vogen value object.</summary>
    /// <param name="type">The candidate type.</param>
    /// <returns><see langword="true"/> for a Vogen value object.</returns>
    public static bool IsValueObject(ITypeSymbol type) => GetPrimitive(type) is not null;
}
