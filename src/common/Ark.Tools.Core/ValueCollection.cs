// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections;
using System.Collections.ObjectModel;

namespace Ark.Tools.Core;

public sealed class ValueCollection<T> : Collection<T?>, IEquatable<ValueCollection<T>>, IFormattable
{
    private readonly IEqualityComparer<T?> _equalityComparer;

    public ValueCollection() : this(new List<T?>()) { }

    public ValueCollection(IEqualityComparer<T?>? equalityComparer = null) : this(new List<T?>(), equalityComparer) { }

    public ValueCollection(IList<T?> list, IEqualityComparer<T?>? equalityComparer = null) : base(list) =>
        _equalityComparer = equalityComparer ?? EqualityComparer<T?>.Default;

    public bool Equals(ValueCollection<T>? other)
    {
        if (other is null) return false;

        if (ReferenceEquals(this, other)) return true;

        if (this.Count != other.Count) return false;

        return this.SequenceEqual(other, _equalityComparer);
    }

    public override bool Equals(object? obj) => obj is { } && (ReferenceEquals(this, obj) || obj is ValueCollection<T> coll && Equals(coll));

    public override int GetHashCode() =>
        unchecked(this.Aggregate(0,
            (current, element) => (current * 397) ^ (element is null ? 0 : _equalityComparer.GetHashCode(element))
        ));

    public string ToString(string? format, IFormatProvider? formatProvider)
        => "[" + string.Join(", ", this.Select(e => _formatValue(e, format, formatProvider))) + "]";

    public override string ToString() => ToString(null, CultureInfo.CurrentCulture);

    private static string? _formatValue(object? value, string? format, IFormatProvider? formatProvider) =>
        value switch
        {
            null => "∅",
            string s => $"\"{s}\"",
            char c => $"\'{c}\'",
            IFormattable @if => @if.ToString(format, formatProvider),
            IConvertible ic => ic.ToString(formatProvider),
            IEnumerable<T> ie => "[" + string.Join(", ", ie.Select(e => _formatValue(e, format, formatProvider))) + "]",
            IEnumerable ie => "[" + string.Join(", ", ie.Cast<object>().Select(e => _formatValue(e, null, formatProvider))) + "]",
            _ => value.ToString()
        };
}