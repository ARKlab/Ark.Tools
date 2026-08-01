// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using MessagePack;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Ark.Tools.Core.MessagePack.Tests;

/// <summary>
/// Round-trip tests proving <see cref="EvolvableEnumFormatterResolver"/> preserves defined and
/// unknown numeric values across every closed <see cref="EvolvableEnum{TEnum}"/> instantiation
/// without per-type registration, and fails explicitly for unrepresentable unknown-name values, as
/// required by GEN-12.
/// </summary>
[TestClass]
public sealed class EvolvableEnumFormatterResolverTests
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

    private static readonly MessagePackSerializerOptions s_options =
        MessagePackSerializerOptions.Standard.WithEvolvableEnumSupport();

    /// <summary>Verifies that a defined value round-trips through MessagePack serialization.</summary>
    [TestMethod]
    public void Roundtrips_DefinedValue()
    {
        // Arrange
        var original = EvolvableEnum<Status>.FromValue(Status.Archived);

        // Act
        var bytes = MessagePackSerializer.Serialize(original, s_options);
        var result = MessagePackSerializer.Deserialize<EvolvableEnum<Status>>(bytes, s_options);

        // Assert
        result.Should().Be(original);
        result.Value.Should().Be(Status.Archived);
    }

    /// <summary>Verifies that an unknown numeric value is preserved across MessagePack serialization.</summary>
    [TestMethod]
    public void Roundtrips_UnknownNumber()
    {
        // Arrange
        var original = EvolvableEnum<Status>.FromNumber(999L);

        // Act
        var bytes = MessagePackSerializer.Serialize(original, s_options);
        var result = MessagePackSerializer.Deserialize<EvolvableEnum<Status>>(bytes, s_options);

        // Assert
        result.IsDefined.Should().BeFalse();
        result.ToInt64().Should().Be(999);
    }

    /// <summary>Verifies that a ulong-backed enum's maximum value round-trips without sign corruption.</summary>
    [TestMethod]
    public void Roundtrips_ULongMaxValue_WithoutSignCorruption()
    {
        // Arrange
        var original = EvolvableEnum<ULongStatus>.FromValue(ULongStatus.Huge);

        // Act
        var bytes = MessagePackSerializer.Serialize(original, s_options);
        var result = MessagePackSerializer.Deserialize<EvolvableEnum<ULongStatus>>(bytes, s_options);

        // Assert
        result.Value.Should().Be(ULongStatus.Huge);
        result.ToUInt64().Should().Be(ulong.MaxValue);
    }

    /// <summary>
    /// Verifies that serializing an unknown-name-only value (with no numeric representation) fails
    /// explicitly instead of silently corrupting the value, since MessagePack has no way to carry a name.
    /// MessagePack wraps formatter exceptions in <see cref="MessagePackSerializationException"/>, so the
    /// original explicit-fail exception is asserted as the inner exception.
    /// </summary>
    [TestMethod]
    public void Serializing_UnknownName_ShouldThrowExplicitly()
    {
        // Arrange
        var original = EvolvableEnum<Status>.FromName("FutureMember");

        // Act
        var act = () => MessagePackSerializer.Serialize(original, s_options);

        // Assert
        act.Should().Throw<MessagePackSerializationException>()
            .WithInnerException<EvolvableEnumConversionException>();
    }

    /// <summary>
    /// Verifies that the resolver supports different closed <see cref="EvolvableEnum{TEnum}"/>
    /// instantiations from a single shared resolver instance, without per-type registration.
    /// </summary>
    [TestMethod]
    public void Resolver_SupportsMultipleClosedGenericTypes_WithoutPerTypeRegistration()
    {
        // Arrange
        var status = EvolvableEnum<Status>.FromValue(Status.Active);
        var uLongStatus = EvolvableEnum<ULongStatus>.FromValue(ULongStatus.Huge);

        // Act
        var statusBytes = MessagePackSerializer.Serialize(status, s_options);
        var uLongStatusBytes = MessagePackSerializer.Serialize(uLongStatus, s_options);

        // Assert
        MessagePackSerializer.Deserialize<EvolvableEnum<Status>>(statusBytes, s_options).Should().Be(status);
        MessagePackSerializer.Deserialize<EvolvableEnum<ULongStatus>>(uLongStatusBytes, s_options).Should().Be(uLongStatus);
    }
}
