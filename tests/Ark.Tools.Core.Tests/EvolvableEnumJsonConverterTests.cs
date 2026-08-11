// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Text.Json;

namespace Ark.Tools.Core.Tests;

/// <summary>
/// Tests for the <see cref="EvolvableEnum{TEnum}"/> System.Text.Json converters: default string
/// representation, explicit opt-in numeric representation, and the unknown-value/cross-form
/// semantics required by GEN-12.
/// </summary>
[TestClass]
public class EvolvableEnumJsonConverterTests
{
    private enum Status
    {
        NOT_SET = 0,
        Active = 1,
        Archived = 2,
    }

    private static JsonSerializerOptions CreateDefaultOptions() => new JsonSerializerOptions().ConfigureArkDefaults();

    private static JsonSerializerOptions CreateIntegerOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new SystemTextJson.EvolvableEnumIntegerJsonConverterFactory());
        return options;
    }

    /// <summary>Verifies that a defined value is serialized as its symbolic name by default.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldSerializeDefinedValueAsName()
    {
        // Arrange
        var options = CreateDefaultOptions();
        EvolvableEnum<Status> value = Status.Active;

        // Act
        var json = JsonSerializer.Serialize(value, options);

        // Assert
        json.Should().Be("\"Active\"");
    }

    /// <summary>Verifies that the default converter round-trips a defined value.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldRoundtripDefinedValue()
    {
        // Arrange
        var options = CreateDefaultOptions();
        EvolvableEnum<Status> value = Status.Archived;

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var result = JsonSerializer.Deserialize<EvolvableEnum<Status>>(json, options);

        // Assert
        result.Should().Be(value);
    }

    /// <summary>Verifies that an unrecognized JSON string is preserved as an unknown-name value and round-trips.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldRoundtripUnknownName()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act
        var result = JsonSerializer.Deserialize<EvolvableEnum<Status>>("\"FutureMember\"", options);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        result.IsDefined.Should().BeFalse();
        result.Name.Should().Be("FutureMember");
        json.Should().Be("\"FutureMember\"");
    }

    /// <summary>Verifies that the default converter defensively accepts a numeric JSON token too.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldAcceptNumericToken()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act
        var result = JsonSerializer.Deserialize<EvolvableEnum<Status>>("1", options);

        // Assert
        result.Value.Should().Be(Status.Active);
    }

    /// <summary>
    /// Verifies that serializing an unknown numeric-only value (no symbolic name, e.g. produced by
    /// an upstream binary transport) fails explicitly instead of silently corrupting the value.
    /// </summary>
    [TestMethod]
    public void DefaultFormat_SerializingUnknownNumber_ShouldThrowExplicitly()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var value = EvolvableEnum<Status>.FromNumber(999);

        // Act
        var act = () => JsonSerializer.Serialize(value, options);

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }

    /// <summary>Verifies that the opt-in integer converter serializes a defined value as a JSON number.</summary>
    [TestMethod]
    public void IntegerFormat_ShouldSerializeDefinedValueAsNumber()
    {
        // Arrange
        var options = CreateIntegerOptions();
        EvolvableEnum<Status> value = Status.Archived;

        // Act
        var json = JsonSerializer.Serialize(value, options);

        // Assert
        json.Should().Be("2");
    }

    /// <summary>Verifies that the opt-in integer converter round-trips a defined value.</summary>
    [TestMethod]
    public void IntegerFormat_ShouldRoundtripDefinedValue()
    {
        // Arrange
        var options = CreateIntegerOptions();
        EvolvableEnum<Status> value = Status.Active;

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var result = JsonSerializer.Deserialize<EvolvableEnum<Status>>(json, options);

        // Assert
        result.Should().Be(value);
    }

    /// <summary>Verifies that the opt-in integer converter preserves an unknown numeric value.</summary>
    [TestMethod]
    public void IntegerFormat_ShouldRoundtripUnknownNumber()
    {
        // Arrange
        var options = CreateIntegerOptions();

        // Act
        var result = JsonSerializer.Deserialize<EvolvableEnum<Status>>("999", options);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        result.IsDefined.Should().BeFalse();
        result.ToNumber().Should().Be(999);
        json.Should().Be("999");
    }

    /// <summary>
    /// Verifies that serializing an unknown-name-only value with the integer converter fails
    /// explicitly instead of silently corrupting the value.
    /// </summary>
    [TestMethod]
    public void IntegerFormat_SerializingUnknownName_ShouldThrowExplicitly()
    {
        // Arrange
        var options = CreateIntegerOptions();
        var value = EvolvableEnum<Status>.FromName("FutureMember");

        // Act
        var act = () => JsonSerializer.Serialize(value, options);

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }

    /// <summary>Verifies that the default (NOT_SET) value serializes as its declared name.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldSerializeNotSetAsName()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var value = default(EvolvableEnum<Status>);

        // Act
        var json = JsonSerializer.Serialize(value, options);

        // Assert
        json.Should().Be("\"NOT_SET\"");
    }
}
