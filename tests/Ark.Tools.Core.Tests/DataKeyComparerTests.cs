// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core.DataKey;

using AwesomeAssertions;

namespace Ark.Tools.Core.Tests;

/// <summary>Tests the hash and equality of <see cref="DataKeyComparer{T}"/>.</summary>
[TestClass]
public class DataKeyComparerTests
{
    /// <summary>A row with a nullable key after a non-null one.</summary>
    public sealed class Row
    {
        /// <summary>The first key.</summary>
        [DataKey]
        public int Id { get; init; }

        /// <summary>The last key, which can be null.</summary>
        [DataKey]
        public string? Code { get; init; }

        /// <summary>A value that is not part of the key.</summary>
        public int Value { get; init; }
    }

    /// <summary>Verifies that a null key does not discard the hash of the keys before it.</summary>
    [TestMethod]
    public void GetHashCode_KeysDifferingBeforeNullKey_HashDifferently()
    {
        var comparer = new DataKeyComparer<Row>();

        var first = comparer.GetHashCode(new Row { Id = 1, Code = null });
        var second = comparer.GetHashCode(new Row { Id = 2, Code = null });

        first.Should().NotBe(second);
    }

    /// <summary>Verifies that rows with equal keys, including a null key, are equal and hash equally.</summary>
    [TestMethod]
    [DataRow(null)]
    [DataRow("A")]
    public void EqualKeys_AreEqualAndHashEqually(string? code)
    {
        var comparer = new DataKeyComparer<Row>();
        var x = new Row { Id = 1, Code = code, Value = 1 };
        var y = new Row { Id = 1, Code = code, Value = 2 };

        comparer.Equals(x, y).Should().BeTrue();
        comparer.GetHashCode(x).Should().Be(comparer.GetHashCode(y));
    }
}
