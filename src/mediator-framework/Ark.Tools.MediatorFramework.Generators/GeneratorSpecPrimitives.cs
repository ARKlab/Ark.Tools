// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>
/// An immutable list of values that compares structurally, so generator specifications carrying
/// collections stay value-equatable and therefore cacheable between incremental runs.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    // Explicit element comparer: ImmutableArray<T> compares by reference, which would defeat caching.
    private static readonly IEqualityComparer<T> _elementComparer = EqualityComparer<T>.Default;

    private readonly ImmutableArray<T> _values;

    /// <summary>Initializes a new instance of the <see cref="EquatableArray{T}"/> struct.</summary>
    /// <param name="values">The values to wrap.</param>
    public EquatableArray(ImmutableArray<T> values)
    {
        _values = values.IsDefault ? ImmutableArray<T>.Empty : values;
    }

    /// <summary>Gets an empty array.</summary>
    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    /// <summary>Gets the wrapped values.</summary>
    public ImmutableArray<T> Values => _values.IsDefault ? ImmutableArray<T>.Empty : _values;

    /// <summary>Gets the number of values.</summary>
    public int Count => Values.Length;

    /// <summary>Gets a value indicating whether the array has no values.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>Gets the value at the requested position.</summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>The value at <paramref name="index"/>.</returns>
    public T this[int index] => Values[index];

    /// <summary>Wraps an immutable array.</summary>
    /// <param name="values">The values to wrap.</param>
    public static implicit operator EquatableArray<T>(ImmutableArray<T> values) => new(values);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Values).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public bool Equals(EquatableArray<T> other)
    {
        var left = Values;
        var right = other.Values;
        if (left.Length != right.Length)
            return false;

        for (var index = 0; index < left.Length; index++)
        {
            if (!_elementComparer.Equals(left[index], right[index]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is EquatableArray<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in Values)
                hash = (hash * 31) + (value is null ? 0 : _elementComparer.GetHashCode(value));
            return hash;
        }
    }
}

/// <summary>Creates <see cref="EquatableArray{T}"/> values from common sequences.</summary>
internal static class EquatableArrayExtensions
{
    /// <summary>Wraps a sequence into a structurally comparable array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="values">The values to wrap.</param>
    /// <returns>The structurally comparable array.</returns>
    public static EquatableArray<T> _toEquatableArray<T>(this IEnumerable<T> values)
        => new(values as ImmutableArray<T>? ?? values.ToImmutableArray());
}

/// <summary>
/// Treats compilations as equal when their assembly name and metadata references are the same, so a
/// pipeline step that only reads referenced (metadata) symbols reruns when references change instead of
/// on every source edit. Steps using this comparer must not read source symbols from the compilation.
/// </summary>
internal sealed class MetadataReferencesComparer : IEqualityComparer<Compilation>
{
    private MetadataReferencesComparer()
    {
    }

    /// <summary>Gets the shared comparer instance.</summary>
    public static MetadataReferencesComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(Compilation? x, Compilation? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return string.Equals(x.AssemblyName, y.AssemblyName, StringComparison.Ordinal)
            && x.References.SequenceEqual(y.References);
    }

    /// <inheritdoc />
    public int GetHashCode(Compilation obj)
        => obj.AssemblyName is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.AssemblyName);
}

/// <summary>Builds the input of pipeline steps that read only referenced (metadata) symbols.</summary>
internal static class ReferenceScope
{
    /// <summary>
    /// Projects each compilation to a copy without syntax trees, compared by <see cref="MetadataReferencesComparer"/>,
    /// so the steps rerun only when references change. An input compared equal keeps the previous item, and keeping
    /// the compilation itself would pin its syntax trees and source symbols until references change. The copy shares
    /// the bound references, so it sees the same metadata symbols.
    /// </summary>
    /// <param name="compilations">The compilation provider.</param>
    /// <returns>The references-only compilation provider.</returns>
    public static IncrementalValueProvider<Compilation> _referencesOnly(this IncrementalValueProvider<Compilation> compilations)
        => compilations
            .Select(static (compilation, _) => compilation.RemoveAllSyntaxTrees())
            .WithComparer(MetadataReferencesComparer.Instance);
}

/// <summary>A symbol-free description of a source location carried by generator specifications.</summary>
internal readonly struct LocationSpec : IEquatable<LocationSpec>
{
    private LocationSpec(
        string filePath,
        int start,
        int length,
        int startLine,
        int startCharacter,
        int endLine,
        int endCharacter)
    {
        FilePath = filePath;
        Start = start;
        Length = length;
        StartLine = startLine;
        StartCharacter = startCharacter;
        EndLine = endLine;
        EndCharacter = endCharacter;
    }

    /// <summary>Gets the file that declares the described syntax.</summary>
    public string FilePath { get; }

    /// <summary>Gets the zero-based source offset.</summary>
    public int Start { get; }

    /// <summary>Gets the source span length.</summary>
    public int Length { get; }

    /// <summary>Gets the zero-based starting line.</summary>
    public int StartLine { get; }

    /// <summary>Gets the zero-based starting character.</summary>
    public int StartCharacter { get; }

    /// <summary>Gets the zero-based ending line.</summary>
    public int EndLine { get; }

    /// <summary>Gets the zero-based ending character.</summary>
    public int EndCharacter { get; }

    /// <summary>Projects a location into a symbol-free specification.</summary>
    /// <param name="location">The location to project.</param>
    /// <returns>The specification, or <see langword="null"/> when the location has no source.</returns>
    public static LocationSpec? _from(Location? location)
    {
        if (location is null || location.SourceTree is null)
            return null;

        var mappedLineSpan = location.GetMappedLineSpan();
        var lineSpan = mappedLineSpan.Span;
        return new LocationSpec(
            string.IsNullOrEmpty(mappedLineSpan.Path) ? location.SourceTree.FilePath : mappedLineSpan.Path,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            lineSpan.Start.Line,
            lineSpan.Start.Character,
            lineSpan.End.Line,
            lineSpan.End.Character);
    }

    /// <summary>Projects the first source location of a symbol.</summary>
    /// <param name="symbol">The symbol to project.</param>
    /// <returns>The specification, or <see langword="null"/> when the symbol has no source location.</returns>
    public static LocationSpec? _from(ISymbol symbol)
        => _from(symbol.Locations.FirstOrDefault(static location => location.IsInSource));

    /// <summary>Projects the span of a syntax reference.</summary>
    /// <param name="reference">The reference to project.</param>
    /// <returns>The specification, or <see langword="null"/> when the reference is missing.</returns>
    public static LocationSpec? _from(SyntaxReference? reference)
        => reference is null ? null : _from(Location.Create(reference.SyntaxTree, reference.Span));

    /// <summary>Materializes a diagnostic location from a specification.</summary>
    /// <param name="spec">The specification to materialize.</param>
    /// <returns>The materialized location, or <see cref="Location.None"/> when no span is known.</returns>
    public static Location _toLocation(LocationSpec? spec)
        => spec is { } value
            ? Location.Create(
                value.FilePath,
                new TextSpan(value.Start, value.Length),
                new LinePositionSpan(
                    new LinePosition(value.StartLine, value.StartCharacter),
                    new LinePosition(value.EndLine, value.EndCharacter)))
            : Location.None;

    /// <inheritdoc />
    public bool Equals(LocationSpec other)
        => string.Equals(FilePath, other.FilePath, StringComparison.Ordinal)
            && Start == other.Start
            && Length == other.Length
            && StartLine == other.StartLine
            && StartCharacter == other.StartCharacter
            && EndLine == other.EndLine
            && EndCharacter == other.EndCharacter;

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is LocationSpec other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = FilePath is null ? 0 : StringComparer.Ordinal.GetHashCode(FilePath);
            hash = (hash * 31) + Start;
            hash = (hash * 31) + Length;
            hash = (hash * 31) + StartLine;
            hash = (hash * 31) + StartCharacter;
            hash = (hash * 31) + EndLine;
            return (hash * 31) + EndCharacter;
        }
    }
}

/// <summary>A symbol-free description of a diagnostic produced while parsing generator inputs.</summary>
/// <param name="DescriptorId">The diagnostic identifier.</param>
/// <param name="Location">The reported location.</param>
/// <param name="Arguments">The message arguments.</param>
internal readonly record struct DiagnosticSpec(
    string DescriptorId,
    LocationSpec? Location,
    EquatableArray<string> Arguments)
{
    /// <summary>Materializes the described diagnostic.</summary>
    /// <param name="resolveDescriptor">Resolves a diagnostic identifier at the output boundary.</param>
    /// <returns>The diagnostic to report.</returns>
    public Diagnostic _toDiagnostic(Func<string, DiagnosticDescriptor> resolveDescriptor)
        => Diagnostic.Create(
            resolveDescriptor(DescriptorId),
            LocationSpec._toLocation(Location),
            Arguments.Values.Cast<object?>().ToArray());
}

/// <summary>Normalizes generated source text so emitted files never depend on the host platform.</summary>
internal static class GeneratedSourceText
{
    /// <summary>Materializes generated source using LF line endings.</summary>
    /// <param name="builder">The builder holding the generated source.</param>
    /// <returns>The generated source with explicit LF line endings.</returns>
    public static string _toGeneratedSource(this StringBuilder builder)
        => builder.Replace("\r\n", "\n").Replace('\r', '\n').ToString();
}

/// <summary>Compares symbol names without formatting display strings.</summary>
internal static class SymbolNames
{
    /// <summary>
    /// Allocation-free equivalent of <c>type.ToDisplayString() == fullName</c> for a non-generic type (nested types
    /// included), such as an attribute class. Generators match the attributes of every member they visit, and
    /// formatting a display string for each attribute was most of that cost. Generic types never match.
    /// </summary>
    /// <param name="type">The type to test, usually <see cref="AttributeData.AttributeClass"/>.</param>
    /// <param name="fullName">The namespace-qualified name, for example <c>Ark.Tools.MediatorFramework.MessageAttribute</c>.</param>
    /// <returns><see langword="true"/> when the type has exactly that name.</returns>
    public static bool _isFullName(this INamedTypeSymbol? type, string fullName)
    {
        if (type is null)
            return false;

        // Match each containing type, then each namespace, from the end of the name backwards.
        var end = fullName.Length;
        for (ISymbol? symbol = type; symbol is not null and not INamespaceSymbol { IsGlobalNamespace: true }; symbol = symbol.ContainingSymbol)
        {
            if (symbol is INamedTypeSymbol { Arity: > 0 })
                return false;

            var name = symbol.Name;
            var start = end - name.Length;
            if (start < 0 || string.CompareOrdinal(fullName, start, name, 0, name.Length) != 0)
                return false;

            end = start - 1;
            if (end >= 0 && fullName[end] != '.')
                return false;
        }

        return end == -1;
    }

    /// <summary>
    /// Allocation-free equivalent of <c>@namespace?.ToDisplayString() == fullName</c> for a namespace name such as
    /// <c>Ark.Tools.Solid</c>.
    /// </summary>
    /// <param name="namespace">The namespace to test, usually a <see cref="ISymbol.ContainingNamespace"/>.</param>
    /// <param name="fullName">The dotted namespace name.</param>
    /// <returns><see langword="true"/> when the namespace has exactly that name.</returns>
    public static bool _isNamespace(this INamespaceSymbol? @namespace, string fullName)
    {
        // Match each namespace from the end of the name backwards; the global namespace never matches.
        var end = fullName.Length;
        for (; @namespace is { IsGlobalNamespace: false }; @namespace = @namespace.ContainingNamespace)
        {
            var name = @namespace.Name;
            var start = end - name.Length;
            if (start < 0 || string.CompareOrdinal(fullName, start, name, 0, name.Length) != 0)
                return false;

            end = start - 1;
            if (end >= 0 && fullName[end] != '.')
                return false;
        }

        return end == -1;
    }

    /// <summary>
    /// Allocation-free match of a type that is not nested by namespace and metadata name, which carries the arity of a
    /// generic type. It replaces comparing the display string of a generic definition, such as
    /// <c>System.IParsable&lt;TSelf&gt;</c>, with <c>_isMetadataName("System", "IParsable`1")</c>.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <param name="namespaceName">The dotted namespace name.</param>
    /// <param name="metadataName">The metadata name, for example <c>IParsable`1</c>.</param>
    /// <returns><see langword="true"/> when the type has that namespace and metadata name.</returns>
    public static bool _isMetadataName(this INamedTypeSymbol? type, string namespaceName, string metadataName)
        => type is { ContainingType: null }
            && string.Equals(type.MetadataName, metadataName, StringComparison.Ordinal)
            && type.ContainingNamespace._isNamespace(namespaceName);
}
