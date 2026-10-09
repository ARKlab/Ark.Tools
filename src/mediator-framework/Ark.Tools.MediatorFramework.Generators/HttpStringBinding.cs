// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>
/// Classifies HTTP contract property types by how a host converts them from a route or query string value,
/// so the Minimal API and Azure Functions generators apply the same rules.
/// </summary>
internal static class HttpStringBinding
{
    /// <summary>
    /// Mirrors the types ASP.NET Minimal API binds from a route or query string without help: primitives, enums,
    /// <c>Uri</c>, <c>StringValues</c>, types with a static <c>TryParse</c> or <c>IParsable&lt;T&gt;</c>, their
    /// nullable forms, and arrays of them.
    /// </summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> when ASP.NET binds the type from a string.</returns>
    public static bool IsStringBindable(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
            return array.ElementType is not IArrayTypeSymbol && IsStringBindable(array.ElementType);

        var targetType = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
        return !RequiresTypeConverterBinding(targetType)
            || targetType.AllInterfaces.Any(candidate =>
                candidate.OriginalDefinition.ToDisplayString() == "System.IParsable<TSelf>"
                && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], targetType));
    }

    /// <summary>Gets whether a single value of the type has no public static <c>TryParse(string, ...)</c>.</summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> when the type has no public static <c>TryParse</c>.</returns>
    public static bool RequiresTypeConverterBinding(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return false;

        var targetType = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
        if (targetType.SpecialType == SpecialType.System_String
            || targetType.TypeKind == TypeKind.Enum
            || targetType.ToDisplayString() is "System.Uri" or "Microsoft.Extensions.Primitives.StringValues")
            return false;

        return !targetType.GetMembers("TryParse")
            .OfType<IMethodSymbol>()
            .Any(method => method.IsStatic
                && method.DeclaredAccessibility == Accessibility.Public
                && method.ReturnType.SpecialType == SpecialType.System_Boolean
                && method.Parameters.Length is 2 or 3
                && method.Parameters[0].Type.SpecialType == SpecialType.System_String
                && (method.Parameters.Length == 2
                    || method.Parameters[1].Type.ToDisplayString() == "System.IFormatProvider")
                && method.Parameters[^1].RefKind == RefKind.Out
                && SymbolEqualityComparer.Default.Equals(method.Parameters[^1].Type, targetType));
    }

    /// <summary>
    /// Gets whether the type is a string collection the generators can build from every value of a query parameter:
    /// <c>string[]</c>, <c>StringValues</c>, or <c>IEnumerable</c>, <c>IReadOnlyCollection</c>, <c>IReadOnlyList</c>,
    /// <c>ICollection</c>, <c>IList</c>, <c>List</c>, <c>ISet</c>, <c>HashSet</c> or <c>ImmutableArray</c> of
    /// <c>string</c>. Any other collection is not convertible from a string.
    /// </summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> for a supported string collection.</returns>
    public static bool IsStringCollection(ITypeSymbol type)
        => type switch
        {
            IArrayTypeSymbol array => array.ElementType.SpecialType == SpecialType.System_String,
            INamedTypeSymbol named when named.ToDisplayString() == "Microsoft.Extensions.Primitives.StringValues" => true,
            INamedTypeSymbol { TypeArguments.Length: 1 } named => named.TypeArguments[0].SpecialType == SpecialType.System_String
                && _stringCollectionShapes.Contains(named.OriginalDefinition.ToDisplayString()),
            _ => false,
        };

    private static readonly HashSet<string> _stringCollectionShapes =
    [
        "System.Collections.Generic.IEnumerable<T>",
        "System.Collections.Generic.IReadOnlyCollection<T>",
        "System.Collections.Generic.IReadOnlyList<T>",
        "System.Collections.Generic.ICollection<T>",
        "System.Collections.Generic.IList<T>",
        "System.Collections.Generic.List<T>",
        "System.Collections.Generic.ISet<T>",
        "System.Collections.Generic.HashSet<T>",
        "System.Collections.Immutable.ImmutableArray<T>",
    ];

    /// <summary>
    /// Gets whether an explicit route or query value binds to the type: a string-bindable type natively, a string
    /// collection from every value, and any other single value through its <c>TypeConverter</c> at runtime. An array
    /// of any other type has no element parser, and no <c>TypeConverter</c> converts a string to a collection or a
    /// complex object, so every request that carries the value would fail.
    /// </summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> when the value binds.</returns>
    public static bool CanBindExplicitly(ITypeSymbol type)
    {
        if (IsStringBindable(type) || IsStringCollection(type))
            return true;
        if (IsComplexOrComplexCollection(type))
            return false;

        return !IsCollection(type) || (type is not IArrayTypeSymbol && HasTypeConverterAttribute(type));
    }

    /// <summary>
    /// Gets whether a route value binds to the type: a route segment is a single value, so arrays and collections,
    /// string collections included, never bind from it.
    /// </summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> when the value binds.</returns>
    public static bool CanBindFromRoute(ITypeSymbol type)
        => !IsCollection(type) && !IsStringCollection(type) && CanBindExplicitly(type);

    /// <summary>Gets whether the type, or the type wrapped by <c>Nullable&lt;T&gt;</c>, is an array or a collection.</summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> for an array or a type implementing <c>IEnumerable</c>, other than <c>string</c>.</returns>
    public static bool IsCollection(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        return type.SpecialType != SpecialType.System_String
            && (type is IArrayTypeSymbol
                || type.SpecialType == SpecialType.System_Collections_IEnumerable
                || type.AllInterfaces.Any(static candidate => candidate.SpecialType == SpecialType.System_Collections_IEnumerable));
    }

    /// <summary>
    /// Gets whether the type is a complex object, or a collection of them. A complex object is a data shape with
    /// public settable or <c>init</c> properties and no string conversion visible at compile time (static
    /// <c>TryParse</c> or <c>[TypeConverter]</c>). Converters registered at runtime through <c>TypeDescriptor</c>,
    /// such as the NodaTime ones, target types without settable properties, so they pass.
    /// </summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> for a complex object or a collection of complex objects.</returns>
    public static bool IsComplexOrComplexCollection(ITypeSymbol type) => _isComplex(type, includeCollections: true);

    /// <summary>Gets whether the type or one of its base types declares <c>[TypeConverter]</c>.</summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> when a <c>[TypeConverter]</c> attribute is declared.</returns>
    public static bool HasTypeConverterAttribute(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetAttributes().Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.ComponentModel.TypeConverterAttribute"))
                return true;
        }

        return false;
    }

    private static bool _isComplex(ITypeSymbol type, bool includeCollections)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        if (type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum || HasTypeConverterAttribute(type))
            return false;

        if (includeCollections)
        {
            var element = type is IArrayTypeSymbol array
                ? array.ElementType
                : _enumerableInterfaces(type).FirstOrDefault()?.TypeArguments[0];
            if (element is not null)
                return _isComplex(element, includeCollections: false);
        }

        return RequiresTypeConverterBinding(type)
            && !HasTypeConverterAttribute(type)
            && type is INamedTypeSymbol named
            && _allProperties(named).Any(static property => !property.IsStatic
                && property.DeclaredAccessibility == Accessibility.Public
                && property.SetMethod is { DeclaredAccessibility: Accessibility.Public });
    }

    private static IEnumerable<INamedTypeSymbol> _enumerableInterfaces(ITypeSymbol type)
        => (type is INamedTypeSymbol named ? type.AllInterfaces.Append(named) : type.AllInterfaces)
            .Where(static candidate => candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

    private static IEnumerable<IPropertySymbol> _allProperties(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                yield return property;
    }
}
