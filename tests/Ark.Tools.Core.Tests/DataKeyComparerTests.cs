// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;

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

        /// <summary>A nullable value-type key.</summary>
        [DataKey]
        public int? Batch { get; init; }

        /// <summary>A value that is not part of the key.</summary>
        public int Value { get; init; }
    }

    /// <summary>A row whose key set includes a static property.</summary>
    public sealed class RowWithStaticKey
    {
        /// <summary>A static key, shared by every row.</summary>
        [DataKey]
        public static int Partition => 7;

        /// <summary>The instance key.</summary>
        [DataKey]
        public int Id { get; init; }
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

    /// <summary>
    /// Verifies the reflection path used without a JIT (Native AOT), which the JIT test run otherwise bypasses:
    /// a null key does not discard the hash of the keys before it, and it agrees with the compiled path.
    /// </summary>
    [TestMethod]
    public void ReflectionPath_KeysDifferingBeforeNullKey_HashDifferentlyAndMatchCompiledPath()
    {
        var hash = typeof(DataKeyComparer<Row>).GetMethod("_reflectionHash", BindingFlags.NonPublic | BindingFlags.Static)!;
        var equals = typeof(DataKeyComparer<Row>).GetMethod("_reflectionEquals", BindingFlags.NonPublic | BindingFlags.Static)!;
        var comparer = new DataKeyComparer<Row>();
        var first = new Row { Id = 1, Code = null };
        var second = new Row { Id = 2, Code = null };

        hash.Invoke(null, [first]).Should().NotBe(hash.Invoke(null, [second]));
        equals.Invoke(null, [first, new Row { Id = 1, Code = null, Value = 3 }]).Should().Be(true);
        equals.Invoke(null, [first, second]).Should().Be(false);
        hash.Invoke(null, [first]).Should().Be(comparer.GetHashCode(first));
    }

    /// <summary>Verifies that a static key property is read without a receiver instead of breaking the comparer.</summary>
    [TestMethod]
    public void StaticKeyProperty_IsSupported()
    {
        var comparer = new DataKeyComparer<RowWithStaticKey>();

        comparer.Equals(new RowWithStaticKey { Id = 1 }, new RowWithStaticKey { Id = 1 }).Should().BeTrue();
        comparer.Equals(new RowWithStaticKey { Id = 1 }, new RowWithStaticKey { Id = 2 }).Should().BeFalse();
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

    /// <summary>Verifies that rows differing in any single key are not equal.</summary>
    [TestMethod]
    [DataRow(2, "A", 1)]
    [DataRow(1, null, 1)]
    [DataRow(1, "B", 1)]
    [DataRow(1, "A", null)]
    [DataRow(1, "A", 2)]
    public void Equals_KeysDiffer_ReturnsFalse(int id, string? code, int? batch)
    {
        var comparer = new DataKeyComparer<Row>();
        var x = new Row { Id = 1, Code = "A", Batch = 1 };
        var y = new Row { Id = id, Code = code, Batch = batch };

        comparer.Equals(x, y).Should().BeFalse();
        comparer.Equals(y, x).Should().BeFalse();
    }
}
