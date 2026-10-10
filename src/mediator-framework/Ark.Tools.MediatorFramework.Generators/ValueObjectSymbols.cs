// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.CodeAnalysis;

using System;
using System.Collections.Immutable;
using System.Linq;

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

    /// <summary>
    /// Gets whether the type is an Ark.Tools.Compliance sensitive value object: a string wrapper whose cleartext is only
    /// reachable through <c>SensitiveValueSerialization</c>. The type implements <c>ISensitiveValue&lt;T&gt;</c> once
    /// compiled, or carries <c>[SensitiveValueObject&lt;string&gt;]</c> in the compilation that declares it.
    /// </summary>
    /// <param name="type">The candidate type.</param>
    /// <returns><see langword="true"/> for a sensitive value object.</returns>
    public static bool IsSensitiveValueObject(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(static contract => contract is { MetadataName: "ISensitiveValue`1" }
                && contract.ContainingNamespace?.ToDisplayString() == "Ark.Tools.Compliance")
            || type.GetAttributes().Any(static attribute => attribute.AttributeClass is { MetadataName: "SensitiveValueObjectAttribute`1" } attributeClass
                && attributeClass.ContainingNamespace?.ToDisplayString() == "Ark.Tools.Compliance");
    }

    /// <summary>
    /// Gets whether Vogen generates <c>TryParse(string, IFormatProvider?, out T)</c> for the value object: it does
    /// unless <c>parsableForPrimitives</c> (or <c>parsableForStrings</c> for a string) is <c>GenerateNothing</c> on the
    /// type's attribute or, when the attribute leaves it unspecified, on the assembly's <c>[VogenDefaults]</c>.
    /// </summary>
    /// <param name="type">A Vogen value object.</param>
    /// <returns><see langword="true"/> when the value object has a generated <c>TryParse</c>.</returns>
    public static bool GeneratesTryParse(ITypeSymbol type)
    {
        var option = GetPrimitive(type) == "string" ? "parsableForStrings" : "parsableForPrimitives";
        var declared = _vogenOption(type.GetAttributes(), "ValueObjectAttribute", option)
            ?? _vogenOption(type.ContainingAssembly?.GetAttributes() ?? default, "VogenDefaultsAttribute", option);
        return declared != "GenerateNothing";
    }

    // The enum member name passed for a Vogen attribute parameter, or null when it is absent or Unspecified.
    private static string? _vogenOption(ImmutableArray<AttributeData> attributes, string attributeName, string parameter)
    {
        foreach (var attribute in attributes.IsDefault ? ImmutableArray<AttributeData>.Empty : attributes)
        {
            if (attribute.AttributeClass is not { } attributeClass
                || attributeClass.ContainingNamespace?.ToDisplayString() != "Vogen"
                || attributeClass.Name != attributeName
                || attribute.AttributeConstructor is not { } constructor)
            {
                continue;
            }

            for (var index = 0; index < constructor.Parameters.Length && index < attribute.ConstructorArguments.Length; index++)
            {
                if (!string.Equals(constructor.Parameters[index].Name, parameter, StringComparison.Ordinal))
                    continue;

                var argument = attribute.ConstructorArguments[index];
                var name = argument.Type?.GetMembers().OfType<IFieldSymbol>()
                    .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, argument.Value))?.Name;
                return name is null or "Unspecified" ? null : name;
            }
        }

        return null;
    }

    /// <summary>Gets whether the type is a Vogen value object.</summary>
    /// <param name="type">The candidate type.</param>
    /// <returns><see langword="true"/> for a Vogen value object.</returns>
    public static bool IsValueObject(ITypeSymbol type) => GetPrimitive(type) is not null;
}
