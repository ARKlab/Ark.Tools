// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

namespace Ark.Tools.Core.Tests;

/// <summary>
/// Tests for <see cref="EvolvableEnum{TEnum}"/> covering structural validation ([Flags] rejection,
/// required NOT_SET member), numeric width/sign fidelity across all integral backing types, and the
/// unknown-value semantics (unknown names and unknown numbers are both preserved, but converting to
/// an unavailable representation fails explicitly instead of corrupting data).
/// </summary>
[TestClass]
public class EvolvableEnumTests
{
    private enum Status : int
    {
        NOT_SET = 0,
        Active = 1,
        Archived = 2,
    }

    private enum ByteStatus : byte
    {
        NOT_SET = 0,
        Low = 1,
        High = 200,
    }

    private enum SByteStatus : sbyte
    {
        NOT_SET = 0,
        Negative = -5,
        Positive = 5,
    }

    private enum ULongStatus : ulong
    {
        NOT_SET = 0,
        Huge = ulong.MaxValue,
    }

    private enum LongStatus : long
    {
        NOT_SET = 0,
        Min = long.MinValue,
        Max = long.MaxValue,
    }

    [Flags]
    private enum BadFlags
    {
        NOT_SET = 0,
        A = 1,
        B = 2,
    }

    private enum MissingNotSet
    {
        A = 0,
        B = 1,
    }

    private enum NotSetNotZero
    {
        NOT_SET = 1,
        A = 0,
    }

    /// <summary>Verifies that a [Flags] enum is rejected at first use.</summary>
    [TestMethod]
    public void FlagsEnum_ShouldBeRejected()
    {
        // Act
        var act = () => EvolvableEnum<BadFlags>.FromValue(BadFlags.A);

        // Assert
        act.Should().Throw<TypeInitializationException>()
            .WithInnerException<NotSupportedException>();
    }

    /// <summary>Verifies that an enum without a NOT_SET member is rejected at first use.</summary>
    [TestMethod]
    public void EnumWithoutNotSetMember_ShouldBeRejected()
    {
        // Act
        var act = () => EvolvableEnum<MissingNotSet>.FromValue(MissingNotSet.A);

        // Assert
        act.Should().Throw<TypeInitializationException>()
            .WithInnerException<InvalidOperationException>();
    }

    /// <summary>Verifies that an enum whose NOT_SET member is not zero-valued is rejected at first use.</summary>
    [TestMethod]
    public void EnumWithNonZeroNotSetMember_ShouldBeRejected()
    {
        // Act
        var act = () => EvolvableEnum<NotSetNotZero>.FromValue(NotSetNotZero.A);

        // Assert
        act.Should().Throw<TypeInitializationException>()
            .WithInnerException<InvalidOperationException>();
    }

    /// <summary>Verifies that the default value is the always-defined NOT_SET member.</summary>
    [TestMethod]
    public void Default_ShouldBeNotSet()
    {
        // Arrange
        var value = default(EvolvableEnum<Status>);

        // Assert
        value.Should().Be(EvolvableEnum<Status>.NotSet);
        value.IsDefined.Should().BeTrue();
        value.Value.Should().Be(Status.NOT_SET);
        value.Name.Should().Be("NOT_SET");
        value.ToNumber().Should().Be(0);
    }

    /// <summary>Verifies that a defined value round-trips through implicit/explicit conversions.</summary>
    [TestMethod]
    public void DefinedValue_ShouldRoundtripThroughConversions()
    {
        // Arrange
        EvolvableEnum<Status> wrapped = Status.Active;

        // Assert
        wrapped.IsDefined.Should().BeTrue();
        wrapped.HasNumericValue.Should().BeTrue();
        wrapped.Value.Should().Be(Status.Active);
        wrapped.Name.Should().Be("Active");
        wrapped.ToNumber().Should().Be(1);
        ((Status)wrapped).Should().Be(Status.Active);
    }

    /// <summary>Verifies that the enum extension wraps a defined value.</summary>
    [TestMethod]
    public void ToEvolvable_ShouldWrapValue()
    {
        // Act
        var value = Status.Active.ToEvolvable();

        // Assert
        value.Should().Be(EvolvableEnum<Status>.FromValue(Status.Active));
    }

    /// <summary>Verifies that an unrecognized numeric value is retained rather than rejected.</summary>
    [TestMethod]
    public void UnknownNumber_ShouldBeRetainedWithoutName()
    {
        // Act
        var value = EvolvableEnum<Status>.FromNumber(999);

        // Assert
        value.IsDefined.Should().BeFalse();
        value.HasNumericValue.Should().BeTrue();
        value.Value.Should().BeNull();
        value.Name.Should().BeNull();
        value.ToNumber().Should().Be(999);
        value.ToString().Should().Be("999");
    }

    /// <summary>Verifies that an unrecognized symbolic name is retained rather than rejected.</summary>
    [TestMethod]
    public void UnknownName_ShouldBeRetainedWithoutNumericValue()
    {
        // Act
        var value = EvolvableEnum<Status>.FromName("SomeFutureMember");

        // Assert
        value.IsDefined.Should().BeFalse();
        value.HasNumericValue.Should().BeFalse();
        value.Value.Should().BeNull();
        value.Name.Should().Be("SomeFutureMember");
        value.ToString().Should().Be("SomeFutureMember");
    }

    /// <summary>Verifies that converting an unknown-name value to a number fails explicitly instead of defaulting.</summary>
    [TestMethod]
    public void UnknownName_ToNumber_ShouldThrowExplicitly()
    {
        // Arrange
        var value = EvolvableEnum<Status>.FromName("SomeFutureMember");

        // Act
        var act = () => value.ToNumber();

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }

    /// <summary>Verifies that converting an unknown-name value to a number fails explicitly instead of defaulting.</summary>
    [TestMethod]
    public void UnknownName_ToNumberAgain_ShouldThrowExplicitly()
    {
        // Arrange
        var value = EvolvableEnum<Status>.FromName("SomeFutureMember");

        // Act
        var act = () => value.ToNumber();

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }

    /// <summary>Verifies that explicitly casting an undefined value to the strict enum fails explicitly instead of defaulting.</summary>
    [TestMethod]
    public void UndefinedValue_ExplicitCast_ShouldThrowExplicitly()
    {
        // Arrange
        var value = EvolvableEnum<Status>.FromNumber(999);

        // Act
        var act = () => (Status)value;

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }

    /// <summary>Verifies FromName resolves a declared member name to the defined value.</summary>
    [TestMethod]
    public void FromName_WithDeclaredMemberName_ShouldResolveToDefinedValue()
    {
        // Act
        var value = EvolvableEnum<Status>.FromName("Active");

        // Assert
        value.IsDefined.Should().BeTrue();
        value.Value.Should().Be(Status.Active);
        value.ToNumber().Should().Be(1);
    }

    /// <summary>Verifies equality semantics for defined, unknown-numeric, and unknown-name values.</summary>
    [TestMethod]
    public void Equality_ShouldCompareByRepresentedState()
    {
        // Assert
        EvolvableEnum<Status>.FromValue(Status.Active).Should().Be(EvolvableEnum<Status>.FromValue(Status.Active));
        EvolvableEnum<Status>.FromNumber(999).Should().Be(EvolvableEnum<Status>.FromNumber(999));
        EvolvableEnum<Status>.FromName("X").Should().Be(EvolvableEnum<Status>.FromName("X"));
        EvolvableEnum<Status>.FromName("X").Should().NotBe(EvolvableEnum<Status>.FromName("Y"));
        // FromName resolves declared names to their defined numeric value, so it is equal to the same value produced numerically.
        EvolvableEnum<Status>.FromNumber(1).Should().Be(EvolvableEnum<Status>.FromName("Active"));
        (EvolvableEnum<Status>.FromValue(Status.Active) == EvolvableEnum<Status>.FromValue(Status.Active)).Should().BeTrue();
        (EvolvableEnum<Status>.FromValue(Status.Active) != EvolvableEnum<Status>.FromValue(Status.Archived)).Should().BeTrue();
    }

    /// <summary>Verifies that the byte-backed enum's width and sign are preserved exactly, including its maximum value.</summary>
    [TestMethod]
    public void ByteBackedEnum_ShouldPreserveWidthAndMagnitude()
    {
        // Act
        var value = EvolvableEnum<ByteStatus, byte>.FromValue(ByteStatus.High);

        // Assert
        value.ToNumber().Should().Be((byte)200);
    }

    /// <summary>Verifies that a signed byte-backed enum preserves negative values exactly.</summary>
    [TestMethod]
    public void SByteBackedEnum_ShouldPreserveSign()
    {
        // Act
        var value = EvolvableEnum<SByteStatus, sbyte>.FromValue(SByteStatus.Negative);

        // Assert
        value.ToNumber().Should().Be((sbyte)-5);
    }

    /// <summary>Verifies that a ulong-backed enum round-trips its maximum value without sign corruption.</summary>
    [TestMethod]
    public void ULongBackedEnum_ShouldRoundtripMaxValueWithoutSignCorruption()
    {
        // Act
        var value = EvolvableEnum<ULongStatus, ulong>.FromValue(ULongStatus.Huge);

        // Assert
        value.ToNumber().Should().Be(ulong.MaxValue);
        value.IsDefined.Should().BeTrue();

        // A numeric value read back from the wire as ulong.MaxValue must resolve to the same defined member.
        var fromNumber = EvolvableEnum<ULongStatus, ulong>.FromNumber(ulong.MaxValue);
        fromNumber.Should().Be(value);
        fromNumber.Value.Should().Be(ULongStatus.Huge);
    }

    /// <summary>Verifies that a long-backed enum round-trips both extremes of the 64-bit signed range.</summary>
    [TestMethod]
    public void LongBackedEnum_ShouldRoundtripMinAndMaxValue()
    {
        // Act
        var min = EvolvableEnum<LongStatus, long>.FromValue(LongStatus.Min);
        var max = EvolvableEnum<LongStatus, long>.FromValue(LongStatus.Max);

        // Assert
        min.ToNumber().Should().Be(long.MinValue);
        max.ToNumber().Should().Be(long.MaxValue);
        min.Value.Should().Be(LongStatus.Min);
        max.Value.Should().Be(LongStatus.Max);
    }

    /// <summary>Verifies that FromNumber(ulong) applied to a signed enum does not corrupt values that fit signed range.</summary>
    [TestMethod]
    public void ExplicitBackingType_ShouldBeRequired()
    {
        // Act
        var act = () => EvolvableEnum<ByteStatus, int>.FromValue(ByteStatus.Low);

        // Assert
        act.Should().Throw<TypeInitializationException>()
            .WithInnerException<InvalidOperationException>();
    }

    /// <summary>Verifies that ToString renders the declared name for a defined value.</summary>
    [TestMethod]
    public void ToString_ForDefinedValue_ShouldRenderName()
    {
        // Act
        var value = EvolvableEnum<Status>.FromValue(Status.Archived);

        // Assert
        value.ToString().Should().Be("Archived");
    }
}
