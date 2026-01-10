// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;
using NodaTime;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Ark.Tools.Nodatime.Tests;

/// <summary>
/// Tests for NullableConverter implementations to ensure TypeConverter functionality
/// doesn't regress when refactoring to generic implementation.
/// </summary>
[TestClass]
public class NullableConverterTests
{
    /// <summary>
    /// Verifies that NullableLocalDateConverter can be instantiated and has correct UnderlyingType.
    /// </summary>
    [TestMethod]
    public void NullableLocalDateConverter_ShouldHaveCorrectUnderlyingType()
    {
        // Arrange & Act
        var converter = new NullableLocalDateConverter();

        // Assert
        converter.Should().NotBeNull();
        var underlyingType = converter.GetType().BaseType?.GetProperty("UnderlyingType")?.GetValue(converter) as Type;
        Assert.AreEqual(typeof(LocalDate), underlyingType);
    }

    /// <summary>
    /// Verifies that NullableLocalDateConverter can convert from string.
    /// </summary>
    [TestMethod]
    public void NullableLocalDateConverter_ShouldConvertFromString()
    {
        // Arrange
        var converter = new NullableLocalDateConverter();

        // Act & Assert
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that NullableLocalTimeConverter can be instantiated and has correct UnderlyingType.
    /// </summary>
    [TestMethod]
    public void NullableLocalTimeConverter_ShouldHaveCorrectUnderlyingType()
    {
        // Arrange & Act
        var converter = new NullableLocalTimeConverter();

        // Assert
        converter.Should().NotBeNull();
        var underlyingType = converter.GetType().BaseType?.GetProperty("UnderlyingType")?.GetValue(converter) as Type;
        Assert.AreEqual(typeof(LocalTime), underlyingType);
    }

    /// <summary>
    /// Verifies that NullableLocalTimeConverter can convert from string.
    /// </summary>
    [TestMethod]
    public void NullableLocalTimeConverter_ShouldConvertFromString()
    {
        // Arrange
        var converter = new NullableLocalTimeConverter();

        // Act & Assert
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that NullableLocalDateTimeConverter can be instantiated and has correct UnderlyingType.
    /// </summary>
    [TestMethod]
    public void NullableLocalDateTimeConverter_ShouldHaveCorrectUnderlyingType()
    {
        // Arrange & Act
        var converter = new NullableLocalDateTimeConverter();

        // Assert
        converter.Should().NotBeNull();
        var underlyingType = converter.GetType().BaseType?.GetProperty("UnderlyingType")?.GetValue(converter) as Type;
        Assert.AreEqual(typeof(LocalDateTime), underlyingType);
    }

    /// <summary>
    /// Verifies that NullableLocalDateTimeConverter can convert from string.
    /// </summary>
    [TestMethod]
    public void NullableLocalDateTimeConverter_ShouldConvertFromString()
    {
        // Arrange
        var converter = new NullableLocalDateTimeConverter();

        // Act & Assert
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that NullableInstantConverter can be instantiated and has correct UnderlyingType.
    /// </summary>
    [TestMethod]
    public void NullableInstantConverter_ShouldHaveCorrectUnderlyingType()
    {
        // Arrange & Act
        var converter = new NullableInstantConverter();

        // Assert
        converter.Should().NotBeNull();
        var underlyingType = converter.GetType().BaseType?.GetProperty("UnderlyingType")?.GetValue(converter) as Type;
        Assert.AreEqual(typeof(Instant), underlyingType);
    }

    /// <summary>
    /// Verifies that NullableInstantConverter can convert from string.
    /// </summary>
    [TestMethod]
    public void NullableInstantConverter_ShouldConvertFromString()
    {
        // Arrange
        var converter = new NullableInstantConverter();

        // Act & Assert
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that NullableOffsetDateTimeConverter can be instantiated and has correct UnderlyingType.
    /// </summary>
    [TestMethod]
    public void NullableOffsetDateTimeConverter_ShouldHaveCorrectUnderlyingType()
    {
        // Arrange & Act
        var converter = new NullableOffsetDateTimeConverter();

        // Assert
        converter.Should().NotBeNull();
        var underlyingType = converter.GetType().BaseType?.GetProperty("UnderlyingType")?.GetValue(converter) as Type;
        Assert.AreEqual(typeof(OffsetDateTime), underlyingType);
    }

    /// <summary>
    /// Verifies that NullableOffsetDateTimeConverter can convert from string.
    /// </summary>
    [TestMethod]
    public void NullableOffsetDateTimeConverter_ShouldConvertFromString()
    {
        // Arrange
        var converter = new NullableOffsetDateTimeConverter();

        // Act & Assert
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
    }
}
