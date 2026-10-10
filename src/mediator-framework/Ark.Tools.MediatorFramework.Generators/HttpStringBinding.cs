// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>How a route or query string value converts to a property value, mirroring Minimal API binding.</summary>
internal enum ConversionKind
{
    /// <summary>The value is a string.</summary>
    String = 0,

    /// <summary><c>Enum.TryParse</c>, case-sensitive as in Minimal API.</summary>
    Enum = 1,

    /// <summary><c>Uri.TryCreate</c>.</summary>
    Uri = 2,

    /// <summary>A public static <c>TryParse(string, IFormatProvider, out T)</c>, called with the invariant culture.</summary>
    TryParseWithProvider = 3,

    /// <summary>A public static <c>TryParse(string, out T)</c>.</summary>
    TryParse = 4,

    /// <summary>An explicit <c>IParsable&lt;T&gt;</c> implementation, called with the invariant culture.</summary>
    Parsable = 5,

    /// <summary>The registered type converter, resolved trim-safely by <c>ArkTypeConverter.TryConvertSafe</c>.</summary>
    TypeConverter = 6,
}

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
        // ASP.NET binds only single-dimensional arrays from repeated values.
        if (type is IArrayTypeSymbol array)
            return array.Rank == 1 && array.ElementType is not IArrayTypeSymbol && IsStringBindable(array.ElementType);

        var targetType = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
        return !RequiresTypeConverterBinding(targetType)
            || targetType.AllInterfaces.Any(candidate =>
                candidate.OriginalDefinition.ToDisplayString() == "System.IParsable<TSelf>"
                && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], targetType));
    }

    /// <summary>
    /// Gets whether the method has the shape of a <c>TryParse</c> a host can call: public, static, non-generic,
    /// returning <see langword="bool"/>, with a by-value <c>string</c> first parameter and an <c>out</c> last one.
    /// </summary>
    /// <param name="method">The candidate method.</param>
    /// <returns><see langword="true"/> for a callable <c>TryParse</c> shape.</returns>
    public static bool IsTryParseShape(IMethodSymbol method)
    {
        return method.IsStatic
            && method.Arity == 0
            && method.DeclaredAccessibility == Accessibility.Public
            && method.ReturnType.SpecialType == SpecialType.System_Boolean
            && method.Parameters.Length is 2 or 3
            && method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && method.Parameters[0].RefKind == RefKind.None
            && method.Parameters[^1].RefKind == RefKind.Out;
    }

    /// <summary>
    /// Gets the <c>TryParse</c> methods declared on the type or inherited from its base types, which ASP.NET Core
    /// also accepts.
    /// </summary>
    /// <param name="type">The candidate type.</param>
    /// <returns>The <c>TryParse</c> methods of the type hierarchy.</returns>
    public static IEnumerable<IMethodSymbol> TryParseMethods(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var method in current.GetMembers("TryParse").OfType<IMethodSymbol>())
                yield return method;
        }
    }

    /// <summary>Gets whether the parameter is a by-value <c>IFormatProvider</c>, nullable or not.</summary>
    /// <param name="parameter">The candidate parameter.</param>
    /// <returns><see langword="true"/> for a by-value <c>IFormatProvider</c> parameter.</returns>
    public static bool IsFormatProviderParameter(IParameterSymbol parameter)
    {
        return parameter.RefKind == RefKind.None
            && parameter.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString() == "System.IFormatProvider";
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

        // Vogen generates TryParse(string, IFormatProvider?, out T) in a source this generator cannot see.
        if (ValueObjectSymbols.IsValueObject(targetType))
            return false;

        return !TryParseMethods(targetType)
            .Any(method => IsTryParseShape(method)
                && (method.Parameters.Length == 2 || IsFormatProviderParameter(method.Parameters[1]))
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
            IArrayTypeSymbol array => array.Rank == 1 && array.ElementType.SpecialType == SpecialType.System_String,
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

        return !IsStringBindable(type)
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

    /// <summary>Gets the strategy that converts a route or query string value to the type.</summary>
    /// <param name="type">The value type.</param>
    /// <returns>The conversion strategy.</returns>
    // Mirrors the order in which Minimal API picks a parser: strings, enums and Uri first, then a public static
    // TryParse (preferring the IFormatProvider overload), then IParsable<T>, and the type converter otherwise.
    public static ConversionKind ConversionKindOf(ITypeSymbol type)
    {
        type = WithoutNullable(type);
        if (type.SpecialType == SpecialType.System_String)
            return ConversionKind.String;
        if (type.TypeKind == TypeKind.Enum)
            return ConversionKind.Enum;
        if (type.ToDisplayString() == "System.Uri")
            return ConversionKind.Uri;
        // Vogen generates TryParse(string, IFormatProvider?, out T) in a source this generator cannot see.
        if (ValueObjectSymbols.IsValueObject(type))
            return ConversionKind.TryParseWithProvider;

        var tryParse = _tryParseMethodsOf(type);
        if (tryParse.Any(_hasFormatProvider))
            return ConversionKind.TryParseWithProvider;
        // As in Minimal API, an explicit IParsable<T> implementation wins over a public TryParse(string, out T).
        if (type.AllInterfaces.Any(candidate => candidate.OriginalDefinition.ToDisplayString() == "System.IParsable<TSelf>"
            && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], type)))
            return ConversionKind.Parsable;
        if (tryParse.Any(static method => method.Parameters.Length == 2))
            return ConversionKind.TryParse;
        return ConversionKind.TypeConverter;
    }

    /// <summary>Gets the type declaring the <c>TryParse</c> method to call.</summary>
    /// <param name="type">The value type.</param>
    /// <returns>The declaring type, or the value type when it has none.</returns>
    // The type declaring the TryParse to call: the most derived declaration, as ASP.NET Core finds it, called on
    // its declaring type so an overload declared on a derived type cannot hide it.
    public static ITypeSymbol ParserType(ITypeSymbol type)
    {
        type = WithoutNullable(type);
        var tryParse = _tryParseMethodsOf(type);
        var method = tryParse.FirstOrDefault(_hasFormatProvider) ?? tryParse.FirstOrDefault(static method => method.Parameters.Length == 2);
        return method?.ContainingType ?? type;
    }

    private static IMethodSymbol[] _tryParseMethodsOf(ITypeSymbol type)
    {
        return TryParseMethods(type)
            .Where(method => IsTryParseShape(method)
                && SymbolEqualityComparer.Default.Equals(method.Parameters[^1].Type, type))
            .ToArray();
    }

    private static bool _hasFormatProvider(IMethodSymbol method)
    {
        return method.Parameters.Length == 3 && IsFormatProviderParameter(method.Parameters[1]);
    }

    /// <summary>Gets the type without <c>Nullable&lt;T&gt;</c> or a nullable annotation.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The underlying type.</returns>
    public static ITypeSymbol WithoutNullable(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
    }

    /// <summary>
    /// Emits the expression converting <paramref name="raw"/> into the out variable <paramref name="value"/> with the
    /// strategy Minimal API picks for the type; it is <see langword="true"/> when conversion succeeds.
    /// </summary>
    /// <param name="kind">The conversion strategy.</param>
    /// <param name="conversionType">The fully qualified type one value converts to.</param>
    /// <param name="parserType">The fully qualified type declaring the <c>TryParse</c> to call.</param>
    /// <param name="raw">The expression of the string value.</param>
    /// <param name="value">The name of the out variable.</param>
    /// <param name="parsableHelper">The generic method calling an <c>IParsable&lt;T&gt;</c> implementation.</param>
    /// <param name="typeConverterHelper">The generic method converting through the registered type converter.</param>
    /// <returns>The conversion expression.</returns>
    public static string TryParseCall(ConversionKind kind, string conversionType, string parserType, string raw, string value, string parsableHelper, string typeConverterHelper)
    {
        return kind switch
        {
            ConversionKind.Enum => "global::System.Enum.TryParse<" + conversionType + ">(" + raw + ", out var " + value + ")",
            ConversionKind.Uri => "global::System.Uri.TryCreate(" + raw + ", global::System.UriKind.RelativeOrAbsolute, out var " + value + ")",
            ConversionKind.TryParseWithProvider => parserType + ".TryParse(" + raw + ", global::System.Globalization.CultureInfo.InvariantCulture, " + _dateTimeStyles(conversionType) + "out var " + value + ")",
            ConversionKind.TryParse => parserType + ".TryParse(" + raw + ", out var " + value + ")",
            ConversionKind.Parsable => parsableHelper + "<" + conversionType + ">(" + raw + ", out var " + value + ")",
            _ => typeConverterHelper + "<" + conversionType + ">(" + raw + ", out var " + value + ")",
        };
    }

    // Mirrors the DateTimeStyles ASP.NET Core passes when it binds a date or time from a string.
    private static string _dateTimeStyles(string type)
    {
        const string styles = "global::System.Globalization.DateTimeStyles.";
        return type switch
        {
            "global::System.DateTime" => styles + "AllowWhiteSpaces | " + styles + "AdjustToUniversal, ",
            "global::System.DateTimeOffset" => styles + "AllowWhiteSpaces | " + styles + "AssumeUniversal, ",
            "global::System.DateOnly" or "global::System.TimeOnly" => styles + "AllowWhiteSpaces, ",
            _ => string.Empty,
        };
    }
}
