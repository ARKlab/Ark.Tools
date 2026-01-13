// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;
using NodaTime;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Ark.Tools.Core.Reflection.Tests;

/// <summary>
/// Tests for EnumerableExtensions.OrderBy to ensure string parsing and ordering work correctly.
/// </summary>
[TestClass]
public class EnumerableExtensionsTests
{
    private sealed class TestEntity
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public OffsetDateTime? CreatedDate { get; set; }
        public NestedEntity? Nested { get; set; }
    }

    private sealed class NestedEntity
    {
        public string? Value { get; set; }
    }

    /// <summary>
    /// Verifies that OrderBy with single property ascending works correctly.
    /// </summary>
    [TestMethod]
    public void OrderBy_SinglePropertyAscending_ShouldSortCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Charlie", Age = 30 },
            new TestEntity { Name = "Alice", Age = 25 },
            new TestEntity { Name = "Bob", Age = 35 }
        };

        // Act
        var result = data.AsQueryable().OrderBy("Name").ToArray();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alice");
        result[1].Name.Should().Be("Bob");
        result[2].Name.Should().Be("Charlie");
    }

    /// <summary>
    /// Verifies that OrderBy with single property descending works correctly.
    /// </summary>
    [TestMethod]
    public void OrderBy_SinglePropertyDescending_ShouldSortCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Charlie", Age = 30 },
            new TestEntity { Name = "Alice", Age = 25 },
            new TestEntity { Name = "Bob", Age = 35 }
        };

        // Act
        var result = data.AsQueryable().OrderBy("Age DESC").ToArray();

        // Assert
        result.Should().HaveCount(3);
        result[0].Age.Should().Be(35);
        result[1].Age.Should().Be(30);
        result[2].Age.Should().Be(25);
    }

    /// <summary>
    /// Verifies that OrderBy with multiple properties works correctly.
    /// </summary>
    [TestMethod]
    public void OrderBy_MultipleProperties_ShouldSortCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Alice", Age = 30 },
            new TestEntity { Name = "Bob", Age = 25 },
            new TestEntity { Name = "Alice", Age = 25 },
            new TestEntity { Name = "Bob", Age = 30 }
        };

        // Act
        var result = data.AsQueryable().OrderBy("Name, Age DESC").ToArray();

        // Assert
        result.Should().HaveCount(4);
        result[0].Name.Should().Be("Alice");
        result[0].Age.Should().Be(30);
        result[1].Name.Should().Be("Alice");
        result[1].Age.Should().Be(25);
        result[2].Name.Should().Be("Bob");
        result[2].Age.Should().Be(30);
        result[3].Name.Should().Be("Bob");
        result[3].Age.Should().Be(25);
    }

    /// <summary>
    /// Verifies that OrderBy with nested properties works correctly.
    /// </summary>
    [TestMethod]
    public void OrderBy_NestedProperty_ShouldSortCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Alice", Nested = new NestedEntity { Value = "Z" } },
            new TestEntity { Name = "Bob", Nested = new NestedEntity { Value = "A" } },
            new TestEntity { Name = "Charlie", Nested = new NestedEntity { Value = "M" } }
        };

        // Act
        var result = data.AsQueryable().OrderBy("Nested.Value").ToArray();

        // Assert
        result.Should().HaveCount(3);
        result[0].Nested!.Value.Should().Be("A");
        result[1].Nested!.Value.Should().Be("M");
        result[2].Nested!.Value.Should().Be("Z");
    }

    /// <summary>
    /// Verifies that OrderBy handles whitespace correctly.
    /// </summary>
    [TestMethod]
    public void OrderBy_WithWhitespace_ShouldHandleCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Charlie", Age = 30 },
            new TestEntity { Name = "Alice", Age = 25 },
            new TestEntity { Name = "Bob", Age = 35 }
        };

        // Act
        var result = data.AsQueryable().OrderBy(" Name , Age DESC ").ToArray();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alice");
        result[1].Name.Should().Be("Bob");
        result[2].Name.Should().Be("Charlie");
    }

    /// <summary>
    /// Verifies that OrderBy with ASC keyword works correctly.
    /// </summary>
    [TestMethod]
    public void OrderBy_WithAscKeyword_ShouldSortCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Charlie", Age = 30 },
            new TestEntity { Name = "Alice", Age = 25 },
            new TestEntity { Name = "Bob", Age = 35 }
        };

        // Act
        var result = data.AsQueryable().OrderBy("Name ASC").ToArray();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alice");
        result[1].Name.Should().Be("Bob");
        result[2].Name.Should().Be("Charlie");
    }

    /// <summary>
    /// Verifies that OrderBy with empty string returns unchanged collection.
    /// </summary>
    [TestMethod]
    public void OrderBy_WithEmptyString_ShouldReturnUnchanged()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Charlie", Age = 30 },
            new TestEntity { Name = "Alice", Age = 25 }
        };

        // Act
        var result = data.AsQueryable().OrderBy("").ToArray();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Charlie");
        result[1].Name.Should().Be("Alice");
    }

    /// <summary>
    /// Verifies that OrderBy throws for invalid property name.
    /// </summary>
    [TestMethod]
    public void OrderBy_WithInvalidProperty_ShouldThrow()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Alice", Age = 25 }
        };

        // Act & Assert
        Action act = () => { var _ = data.AsQueryable().OrderBy("InvalidProperty").ToArray(); };
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Property*InvalidProperty*");
    }

    /// <summary>
    /// Verifies that OrderBy throws for invalid format with too many parts.
    /// </summary>
    [TestMethod]
    public void OrderBy_WithTooManyParts_ShouldThrow()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Alice", Age = 25 }
        };

        // Act & Assert
        Action act = () => { var _ = data.AsQueryable().OrderBy("Name ASC EXTRA").ToArray(); };
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that OrderBy with OffsetDateTime uses proper comparer.
    /// </summary>
    [TestMethod]
    public void OrderBy_WithOffsetDateTime_ShouldSortCorrectly()
    {
        // Arrange
        var offset = Offset.FromHours(2);
        var data = new[]
        {
            new TestEntity { Name = "C", CreatedDate = new OffsetDateTime(new LocalDateTime(2024, 1, 3, 10, 0), offset) },
            new TestEntity { Name = "A", CreatedDate = new OffsetDateTime(new LocalDateTime(2024, 1, 1, 10, 0), offset) },
            new TestEntity { Name = "B", CreatedDate = new OffsetDateTime(new LocalDateTime(2024, 1, 2, 10, 0), offset) }
        };

        // Act
        var result = data.AsQueryable().OrderBy("CreatedDate").ToArray();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("A");
        result[1].Name.Should().Be("B");
        result[2].Name.Should().Be("C");
    }

    /// <summary>
    /// Verifies that OrderBy is case-insensitive for DESC keyword.
    /// </summary>
    [TestMethod]
    public void OrderBy_DescKeywordCaseInsensitive_ShouldWork()
    {
        // Arrange
        var data = new[]
        {
            new TestEntity { Name = "Alice", Age = 25 },
            new TestEntity { Name = "Bob", Age = 35 }
        };

        // Act
        var resultLower = data.AsQueryable().OrderBy("Age desc").ToArray();
        var resultUpper = data.AsQueryable().OrderBy("Age DESC").ToArray();
        var resultMixed = data.AsQueryable().OrderBy("Age DeSc").ToArray();

        // Assert
        resultLower[0].Age.Should().Be(35);
        resultUpper[0].Age.Should().Be(35);
        resultMixed[0].Age.Should().Be(35);
    }
}
