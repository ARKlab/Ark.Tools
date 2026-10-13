// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Runtime.Serialization;
using System.Text.Encodings.Web;
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
    private static readonly JsonSerializerOptions _byteStatusOptions = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new SystemTextJson.EvolvableEnumJsonConverter<ByteStatus, byte>() },
    };

    private static readonly SystemTextJson.EvolvableEnumJsonConverter<Comparison> _sharedComparisonConverter = new();

    private static readonly JsonSerializerOptions _relaxedOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { _sharedComparisonConverter },
    };

    private static readonly JsonSerializerOptions _defaultEncoderOptions = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { _sharedComparisonConverter },
    };

    private enum Comparison
    {
        NOT_SET = 0,
        [EnumMember(Value = "a<b")]
        Less = 1,
    }

    private enum Status
    {
        NOT_SET = 0,
        Active = 1,
        Archived = 2,
    }

    private enum ByteStatus : byte
    {
        NOT_SET = 0,
        Active = 1,
    }

    private enum SByteStatus : sbyte
    {
        Negative = -5,
        NOT_SET = 0,
    }

    private enum ULongStatus : ulong
    {
        NOT_SET = 0,
        Large = ulong.MaxValue,
    }

    private enum LongNamed
    {
        NOT_SET = 0,
        [EnumMember(Value = _longName)]
        Long = 1,
    }

    private enum BlankNamed
    {
        NOT_SET = 0,
        [EnumMember(Value = " ")]
        Blank = 1,
    }

    // Longer than the converters' 128-char stack buffer, so it is read through the allocating path.
    private const string _longName = "long-"
        + "0123456789012345678901234567890123456789"
        + "0123456789012345678901234567890123456789"
        + "0123456789012345678901234567890123456789"
        + "0123456789012345678901234567890123456789";

    private static JsonSerializerOptions _createDefaultOptions() => new JsonSerializerOptions
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    }.ConfigureArkDefaults();

    private static JsonSerializerOptions _createIntegerOptions()
    {
        var options = new JsonSerializerOptions
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true
        };
        options.Converters.Add(new SystemTextJson.EvolvableEnumIntegerJsonConverterFactory());
        return options;
    }

    /// <summary>Verifies that the closed converter for Native AOT reads and writes names like the factory.</summary>
    [TestMethod]
    public void ClosedConverter_ShouldRoundTripLikeTheFactory()
    {
        var options = new JsonSerializerOptions { RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true };
        options.Converters.Add(new SystemTextJson.EvolvableEnumJsonConverter<Status>());

        var json = JsonSerializer.Serialize(EvolvableEnum<Status>.FromValue(Status.Active), options);

        json.Should().Be(JsonSerializer.Serialize(EvolvableEnum<Status>.FromValue(Status.Active), _createDefaultOptions()));
        JsonSerializer.Deserialize<EvolvableEnum<Status>>(json, options).Should().Be(EvolvableEnum<Status>.FromValue(Status.Active));
        JsonSerializer.Deserialize<EvolvableEnum<Status>>("\"Unknown\"", options).Name.Should().Be("Unknown");
    }

    /// <summary>Verifies that the closed two-parameter converter reads and writes names like the factory.</summary>
    [TestMethod]
    public void ClosedBackingConverter_ShouldRoundTripLikeTheFactory()
    {
        var value = EvolvableEnum<ByteStatus, byte>.FromValue(ByteStatus.Active);

        var json = JsonSerializer.Serialize(value, _byteStatusOptions);

        json.Should().Be(JsonSerializer.Serialize(value, _createDefaultOptions()));
        JsonSerializer.Deserialize<EvolvableEnum<ByteStatus, byte>>(json, _byteStatusOptions).Should().Be(value);
        JsonSerializer.Deserialize<EvolvableEnum<ByteStatus, byte>>("\"Unknown\"", _byteStatusOptions).Name.Should().Be("Unknown");
    }

    /// <summary>Verifies that the closed converter honours the encoder of the options it serializes with.</summary>
    [TestMethod]
    public void ClosedConverter_ShouldUseTheOptionsEncoder()
    {
        JsonSerializer.Serialize(EvolvableEnum<Comparison>.FromValue(Comparison.Less), _relaxedOptions).Should().Be("\"a<b\"");
    }

    /// <summary>Verifies that one closed converter instance shared by two options uses each options' encoder.</summary>
    [TestMethod]
    public void ClosedConverter_ShouldUseEachOptionsEncoderWhenShared()
    {
        var value = EvolvableEnum<Comparison>.FromValue(Comparison.Less);
        JsonSerializer.Serialize(value, _relaxedOptions).Should().Be("\"a<b\"");
        JsonSerializer.Serialize(value, _defaultEncoderOptions).Should().Be("\"a\\u003Cb\"");
    }

    /// <summary>Verifies that a defined value is serialized as its symbolic name by default.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldSerializeDefinedValueAsName()
    {
        // Arrange
        var options = _createDefaultOptions();
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
        var options = _createDefaultOptions();
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
        var options = _createDefaultOptions();

        // Act
        var result = JsonSerializer.Deserialize<EvolvableEnum<Status>>("\"FutureMember\"", options);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        result.IsDefined.Should().BeFalse();
        result.Name.Should().Be("FutureMember");
        json.Should().Be("\"FutureMember\"");
    }

    /// <summary>Verifies that exact and escaped declared names are read as the declared value, with both converters.</summary>
    [TestMethod]
    [DataRow("\"Active\"")]
    [DataRow("\"\\u0041ctive\"")]
    public void DefaultFormat_ShouldReadExactAndEscapedNames(string json)
    {
        // Act
        var result = JsonSerializer.Deserialize<EvolvableEnum<Status>>(json, _createDefaultOptions());
        var backed = JsonSerializer.Deserialize<EvolvableEnum<ByteStatus, byte>>(json, _byteStatusOptions);

        // Assert
        result.Value.Should().Be(Status.Active);
        backed.Value.Should().Be(ByteStatus.Active);
    }

    /// <summary>Verifies that case-mismatched and escaped unknown names are preserved as unknown names.</summary>
    [TestMethod]
    [DataRow("\"active\"", "active")]
    [DataRow("\"Future\\u0041\"", "FutureA")]
    public void DefaultFormat_ShouldPreserveCaseMismatchedAndEscapedUnknownNames(string json, string name)
    {
        // Act
        var result = JsonSerializer.Deserialize<EvolvableEnum<Status>>(json, _createDefaultOptions());

        // Assert
        result.IsDefined.Should().BeFalse();
        result.Name.Should().Be(name);
    }

    /// <summary>Verifies that names longer than the stack buffer are read like short ones.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldReadNamesLongerThanTheStackBuffer()
    {
        // Arrange
        var options = _createDefaultOptions();
        var unknown = new string('x', 200);

        // Act
        var known = JsonSerializer.Deserialize<EvolvableEnum<LongNamed>>("\"" + _longName + "\"", options);
        var preserved = JsonSerializer.Deserialize<EvolvableEnum<LongNamed>>("\"" + unknown + "\"", options);

        // Assert
        known.Value.Should().Be(LongNamed.Long);
        preserved.Name.Should().Be(unknown);
    }

    /// <summary>Verifies that member names other than the written name, and members written as blank, are still read.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldReadMemberNamesThatAreNotTheWrittenName()
    {
        // Arrange
        var options = _createDefaultOptions();

        // Act
        var alias = JsonSerializer.Deserialize<EvolvableEnum<LongNamed>>("\"Long\"", options);
        var blank = JsonSerializer.Deserialize<EvolvableEnum<BlankNamed>>("\"Blank\"", options);

        // Assert
        alias.Value.Should().Be(LongNamed.Long);
        blank.Value.Should().Be(BlankNamed.Blank);
    }

    /// <summary>Verifies that an empty name is still rejected.</summary>
    [TestMethod]
    public void DefaultFormat_EmptyName_ShouldThrow()
    {
        // Act
        var act = static () => JsonSerializer.Deserialize<EvolvableEnum<Status>>("\"\"", _createDefaultOptions());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>Verifies that the default converter defensively accepts a numeric JSON token too.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldAcceptNumericToken()
    {
        // Arrange
        var options = _createDefaultOptions();

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
        var options = _createDefaultOptions();
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
        var options = _createIntegerOptions();
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
        var options = _createIntegerOptions();
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
        var options = _createIntegerOptions();

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
        var options = _createIntegerOptions();
        var value = EvolvableEnum<Status>.FromName("FutureMember");

        // Act
        var act = () => JsonSerializer.Serialize(value, options);

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }

    /// <summary>Numbers round-trip for non-int backing types, including the extremes of each range.</summary>
    [TestMethod]
    public void IntegerFormat_ShouldRoundtripNumbersForEveryBackingType()
    {
        var options = _createIntegerOptions();

        JsonSerializer.Deserialize<EvolvableEnum<ByteStatus, byte>[]>("[1,255]", options)!
            .Select(static v => v.ToNumber()).Should().Equal((byte)1, (byte)255);
        JsonSerializer.Deserialize<EvolvableEnum<SByteStatus, sbyte>[]>("[-5,-128,127]", options)!
            .Select(static v => v.ToNumber()).Should().Equal((sbyte)-5, sbyte.MinValue, sbyte.MaxValue);
        JsonSerializer.Deserialize<EvolvableEnum<ULongStatus, ulong>[]>("[18446744073709551615]", options)!
            .Single().ToNumber().Should().Be(ulong.MaxValue);
        JsonSerializer.Serialize(EvolvableEnum<SByteStatus, sbyte>.FromNumber(-5), options).Should().Be("-5");
    }

    /// <summary>A number outside the backing type's range is rejected rather than truncated.</summary>
    [TestMethod]
    public void IntegerFormat_ShouldRejectNumbersOutsideTheBackingRange()
    {
        var options = _createIntegerOptions();

        var tooLarge = () => JsonSerializer.Deserialize<EvolvableEnum<ByteStatus, byte>>("256", options);
        var tooSmall = () => JsonSerializer.Deserialize<EvolvableEnum<SByteStatus, sbyte>>("-129", options);

        tooLarge.Should().Throw<OverflowException>();
        tooSmall.Should().Throw<OverflowException>();
    }

    /// <summary>Verifies that the default (NOT_SET) value serializes as its declared name.</summary>
    [TestMethod]
    public void DefaultFormat_ShouldSerializeNotSetAsName()
    {
        // Arrange
        var options = _createDefaultOptions();
        var value = default(EvolvableEnum<Status>);

        // Act
        var json = JsonSerializer.Serialize(value, options);

        // Assert
        json.Should().Be("\"NOT_SET\"");
    }
}
