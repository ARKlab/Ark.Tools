// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Data;

namespace Ark.Tools.Core.Dapper.Tests;

/// <summary>
/// Tests for <see cref="EvolvableEnumTypeHandler{TEnum}"/> covering the default symbolic-name SQL
/// wire format, the opt-in numeric format, and the unknown-value/cross-form semantics required by
/// GEN-12.
/// </summary>
[TestClass]
public class EvolvableEnumTypeHandlerTests
{
    private enum Status
    {
        NOT_SET = 0,
        Active = 1,
        Archived = 2,
    }

    private enum ULongStatus : ulong
    {
        NOT_SET = 0,
        Huge = ulong.MaxValue,
    }

    /// <summary>Verifies that the default (Name) handler writes the symbolic name.</summary>
    [TestMethod]
    public void SetValue_NameFormat_ShouldWriteSymbolicName()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>();
        var parameter = new FakeDbDataParameter();

        // Act
        handler.SetValue(parameter, EvolvableEnum<Status>.FromValue(Status.Active));

        // Assert
        parameter.Value.Should().Be("Active");
    }

    /// <summary>Verifies that writing an unknown numeric-only value with the Name format fails explicitly.</summary>
    [TestMethod]
    public void SetValue_NameFormat_UnknownNumber_ShouldThrowExplicitly()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>();
        var parameter = new FakeDbDataParameter();

        // Act
        var act = () => handler.SetValue(parameter, EvolvableEnum<Status>.FromNumber(999L));

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }

    /// <summary>Verifies that the opt-in Number handler writes the underlying numeric value.</summary>
    [TestMethod]
    public void SetValue_NumberFormat_ShouldWriteNumericValue()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>(EvolvableEnumWireFormat.Number);
        var parameter = new FakeDbDataParameter();

        // Act
        handler.SetValue(parameter, EvolvableEnum<Status>.FromValue(Status.Archived));

        // Assert
        parameter.Value.Should().Be(2L);
    }

    /// <summary>Verifies that a ulong value beyond long.MaxValue is written as decimal to avoid sign corruption.</summary>
    [TestMethod]
    public void SetValue_NumberFormat_HugeULong_ShouldWriteAsDecimalWithoutSignCorruption()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<ULongStatus>(EvolvableEnumWireFormat.Number);
        var parameter = new FakeDbDataParameter();

        // Act
        handler.SetValue(parameter, EvolvableEnum<ULongStatus>.FromValue(ULongStatus.Huge));

        // Assert
        parameter.Value.Should().Be((decimal)ulong.MaxValue);
    }

    /// <summary>Verifies that writing an unknown-name-only value with the Number format fails explicitly.</summary>
    [TestMethod]
    public void SetValue_NumberFormat_UnknownName_ShouldThrowExplicitly()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>(EvolvableEnumWireFormat.Number);
        var parameter = new FakeDbDataParameter();

        // Act
        var act = () => handler.SetValue(parameter, EvolvableEnum<Status>.FromName("FutureMember"));

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }

    /// <summary>Verifies that a null or DBNull column value parses to NOT_SET.</summary>
    [TestMethod]
    public void Parse_Null_ShouldReturnNotSet()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>();

        // Assert
        handler.Parse(null).Should().Be(EvolvableEnum<Status>.NotSet);
        handler.Parse(DBNull.Value).Should().Be(EvolvableEnum<Status>.NotSet);
    }

    /// <summary>Verifies that a declared member name parses to the defined value.</summary>
    [TestMethod]
    public void Parse_KnownString_ShouldReturnDefinedValue()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>();

        // Act
        var value = handler.Parse("Active");

        // Assert
        value.Value.Should().Be(Status.Active);
    }

    /// <summary>Verifies that an unrecognized column string is retained as an unknown-name value.</summary>
    [TestMethod]
    public void Parse_UnknownString_ShouldBeRetained()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>();

        // Act
        var value = handler.Parse("FutureMember");

        // Assert
        value.IsDefined.Should().BeFalse();
        value.Name.Should().Be("FutureMember");
    }

    /// <summary>Verifies that an integral column value parses to the matching defined value.</summary>
    [TestMethod]
    public void Parse_KnownInteger_ShouldReturnDefinedValue()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>();

        // Act
        var value = handler.Parse(2);

        // Assert
        value.Value.Should().Be(Status.Archived);
    }

    /// <summary>Verifies that an unrecognized numeric column value is retained without a name.</summary>
    [TestMethod]
    public void Parse_UnknownInteger_ShouldBeRetained()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>();

        // Act
        var value = handler.Parse(999L);

        // Assert
        value.IsDefined.Should().BeFalse();
        value.ToInt64().Should().Be(999);
    }

    /// <summary>Verifies that a huge decimal column value round-trips a ulong-backed enum's maximum value.</summary>
    [TestMethod]
    public void Parse_HugeDecimal_ShouldRoundtripULongMaxValue()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<ULongStatus>();

        // Act
        var value = handler.Parse((decimal)ulong.MaxValue);

        // Assert
        value.Value.Should().Be(ULongStatus.Huge);
    }

    /// <summary>Verifies that an unsupported column value type fails with a clear <see cref="DataException"/>.</summary>
    [TestMethod]
    public void Parse_UnsupportedType_ShouldThrowDataException()
    {
        // Arrange
        var handler = new EvolvableEnumTypeHandler<Status>();

        // Act
        var act = () => handler.Parse(true);

        // Assert
        act.Should().Throw<DataException>();
    }

    /// <summary>Verifies that registering a closed EvolvableEnum type does not throw.</summary>
    [TestMethod]
    public void Register_ShouldNotThrow()
    {
        // Act
        var act = () => EvolvableEnumDapper.Register<Status>();

        // Assert
        act.Should().NotThrow();
    }
}
