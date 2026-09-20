// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

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

/// <summary>A symbol-free description of a source location carried by generator specifications.</summary>
internal readonly struct LocationSpec : IEquatable<LocationSpec>
{
    private LocationSpec(string filePath, TextSpan textSpan, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        TextSpan = textSpan;
        LineSpan = lineSpan;
    }

    /// <summary>Gets the file that declares the described syntax.</summary>
    public string FilePath { get; }

    /// <summary>Gets the described span.</summary>
    public TextSpan TextSpan { get; }

    /// <summary>Gets the described line span.</summary>
    public LinePositionSpan LineSpan { get; }

    /// <summary>Projects a location into a symbol-free specification.</summary>
    /// <param name="location">The location to project.</param>
    /// <returns>The specification, or <see langword="null"/> when the location has no source.</returns>
    public static LocationSpec? _from(Location? location)
    {
        if (location is null || location.SourceTree is null)
            return null;

        return new LocationSpec(
            location.SourceTree.FilePath,
            location.SourceSpan,
            location.GetLineSpan().Span);
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
            ? Location.Create(value.FilePath, value.TextSpan, value.LineSpan)
            : Location.None;

    /// <inheritdoc />
    public bool Equals(LocationSpec other)
        => string.Equals(FilePath, other.FilePath, StringComparison.Ordinal)
            && TextSpan.Equals(other.TextSpan)
            && LineSpan.Equals(other.LineSpan);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is LocationSpec other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = FilePath is null ? 0 : StringComparer.Ordinal.GetHashCode(FilePath);
            hash = (hash * 31) + TextSpan.GetHashCode();
            return (hash * 31) + LineSpan.GetHashCode();
        }
    }
}

/// <summary>A symbol-free description of a diagnostic produced while parsing generator inputs.</summary>
/// <param name="Descriptor">The diagnostic descriptor.</param>
/// <param name="Location">The reported location.</param>
/// <param name="Arguments">The message arguments.</param>
internal readonly record struct DiagnosticSpec(
    DiagnosticDescriptor Descriptor,
    LocationSpec? Location,
    EquatableArray<string> Arguments)
{
    /// <summary>Materializes the described diagnostic.</summary>
    /// <returns>The diagnostic to report.</returns>
    public Diagnostic _toDiagnostic()
        => Diagnostic.Create(
            Descriptor,
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
