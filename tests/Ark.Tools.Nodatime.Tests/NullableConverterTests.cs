// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;
using NodaTime;
using System.ComponentModel;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Ark.Tools.Nodatime.Tests;

/// <summary>
/// Tests for NullableConverter implementations to ensure TypeConverter functionality
/// works correctly with roundtrip conversions.
/// </summary>
[TestClass]
public class NullableConverterTests
{
    /// <summary>
    /// Verifies that NullableLocalDateConverter can convert LocalDate to string and back.
    /// </summary>
    [TestMethod]
    public void NullableLocalDateConverter_ShouldRoundtripConversion()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(LocalDate?));
        var original = new LocalDate(2024, 1, 10);

        // Act - Convert to string
        var stringValue = converter.ConvertToString(original);
        stringValue.Should().NotBeNullOrWhiteSpace();

        // Convert back from string
        var converted = converter.ConvertFromString(stringValue);

        // Assert
        converted.Should().BeOfType<LocalDate>();
        ((LocalDate)converted!).Should().Be(original);
    }

    /// <summary>
    /// Verifies that NullableLocalDateConverter handles null values correctly.
    /// </summary>
    [TestMethod]
    public void NullableLocalDateConverter_ShouldHandleNullValue()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(LocalDate?));
        LocalDate? nullValue = null;

        // Act - Convert null to string
        var stringValue = converter.ConvertToString(nullValue);

        // Assert - null should convert to empty string
        stringValue.Should().Be(string.Empty);

        // Act - Convert empty string back
        var converted = converter.ConvertFromString(string.Empty);

        // Assert - should get null back
        converted.Should().BeNull();
    }

    /// <summary>
    /// Verifies that NullableLocalTimeConverter can convert LocalTime to string and back.
    /// </summary>
    [TestMethod]
    public void NullableLocalTimeConverter_ShouldRoundtripConversion()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(LocalTime?));
        var original = new LocalTime(14, 30, 45);

        // Act - Convert to string
        var stringValue = converter.ConvertToString(original);
        stringValue.Should().NotBeNullOrWhiteSpace();

        // Convert back from string
        var converted = converter.ConvertFromString(stringValue);

        // Assert
        converted.Should().BeOfType<LocalTime>();
        ((LocalTime)converted!).Should().Be(original);
    }

    /// <summary>
    /// Verifies that NullableLocalTimeConverter handles null values correctly.
    /// </summary>
    [TestMethod]
    public void NullableLocalTimeConverter_ShouldHandleNullValue()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(LocalTime?));
        LocalTime? nullValue = null;

        // Act
        var stringValue = converter.ConvertToString(nullValue);

        // Assert
        stringValue.Should().Be(string.Empty);
        converter.ConvertFromString(string.Empty).Should().BeNull();
    }

    /// <summary>
    /// Verifies that NullableLocalDateTimeConverter can convert LocalDateTime to string and back.
    /// </summary>
    [TestMethod]
    public void NullableLocalDateTimeConverter_ShouldRoundtripConversion()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(LocalDateTime?));
        var original = new LocalDateTime(2024, 1, 10, 14, 30, 45);

        // Act - Convert to string
        var stringValue = converter.ConvertToString(original);
        stringValue.Should().NotBeNullOrWhiteSpace();

        // Convert back from string
        var converted = converter.ConvertFromString(stringValue);

        // Assert
        converted.Should().BeOfType<LocalDateTime>();
        ((LocalDateTime)converted!).Should().Be(original);
    }

    /// <summary>
    /// Verifies that NullableLocalDateTimeConverter handles null values correctly.
    /// </summary>
    [TestMethod]
    public void NullableLocalDateTimeConverter_ShouldHandleNullValue()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(LocalDateTime?));
        LocalDateTime? nullValue = null;

        // Act
        var stringValue = converter.ConvertToString(nullValue);

        // Assert
        stringValue.Should().Be(string.Empty);
        converter.ConvertFromString(string.Empty).Should().BeNull();
    }

    /// <summary>
    /// Verifies that NullableInstantConverter can convert Instant to string and back.
    /// </summary>
    [TestMethod]
    public void NullableInstantConverter_ShouldRoundtripConversion()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(Instant?));
        var original = Instant.FromUtc(2024, 1, 10, 14, 30, 45);

        // Act - Convert to string
        var stringValue = converter.ConvertToString(original);
        stringValue.Should().NotBeNullOrWhiteSpace();

        // Convert back from string
        var converted = converter.ConvertFromString(stringValue);

        // Assert
        converted.Should().BeOfType<Instant>();
        ((Instant)converted!).Should().Be(original);
    }

    /// <summary>
    /// Verifies that NullableInstantConverter handles null values correctly.
    /// </summary>
    [TestMethod]
    public void NullableInstantConverter_ShouldHandleNullValue()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(Instant?));
        Instant? nullValue = null;

        // Act
        var stringValue = converter.ConvertToString(nullValue);

        // Assert
        stringValue.Should().Be(string.Empty);
        converter.ConvertFromString(string.Empty).Should().BeNull();
    }

    /// <summary>
    /// Verifies that NullableOffsetDateTimeConverter can convert OffsetDateTime to string and back.
    /// </summary>
    [TestMethod]
    public void NullableOffsetDateTimeConverter_ShouldRoundtripConversion()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(OffsetDateTime?));
        var offset = Offset.FromHours(2);
        var original = new OffsetDateTime(new LocalDateTime(2024, 1, 10, 14, 30, 45), offset);

        // Act - Convert to string
        var stringValue = converter.ConvertToString(original);
        stringValue.Should().NotBeNullOrWhiteSpace();

        // Convert back from string
        var converted = converter.ConvertFromString(stringValue);

        // Assert
        converted.Should().BeOfType<OffsetDateTime>();
        ((OffsetDateTime)converted!).Should().Be(original);
    }

    /// <summary>
    /// Verifies that NullableOffsetDateTimeConverter handles null values correctly.
    /// </summary>
    [TestMethod]
    public void NullableOffsetDateTimeConverter_ShouldHandleNullValue()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(OffsetDateTime?));
        OffsetDateTime? nullValue = null;

        // Act
        var stringValue = converter.ConvertToString(nullValue);

        // Assert
        stringValue.Should().Be(string.Empty);
        converter.ConvertFromString(string.Empty).Should().BeNull();
    }
}
