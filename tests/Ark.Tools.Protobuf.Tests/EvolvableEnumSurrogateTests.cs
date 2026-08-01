// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Ark.Tools.Core;

using ProtoBuf;
using ProtoBuf.Meta;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Ark.Tools.Protobuf.Tests;

/// <summary>
/// Round-trip tests proving the <see cref="EvolvableEnumSurrogate{TEnum}"/> open-generic surrogate
/// preserves defined and unknown numeric values, and fails explicitly for unrepresentable
/// unknown-name values, as required by GEN-12.
/// </summary>
[TestClass]
public sealed class EvolvableEnumSurrogateTests
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

    [ProtoContract]
    private sealed class Wrapper
    {
        [ProtoMember(1)]
        public EvolvableEnum<Status> Status { get; set; }
    }

    [ProtoContract]
    private sealed class ULongWrapper
    {
        [ProtoMember(1)]
        public EvolvableEnum<ULongStatus, ulong> Status { get; set; }
    }

    private static RuntimeTypeModel CreateModel()
    {
        var model = RuntimeTypeModel.Create();
        model.AddEvolvableEnumSurrogate<Status>();
        model.AddEvolvableEnumSurrogate<ULongStatus, ulong>();
        model.Add(typeof(Wrapper), true);
        model.Add(typeof(ULongWrapper), true);
        return model;
    }

    /// <summary>Verifies that a defined value round-trips through protobuf serialization.</summary>
    [TestMethod]
    public void Roundtrips_DefinedValue()
    {
        // Arrange
        var model = CreateModel();
        var original = new Wrapper { Status = EvolvableEnum<Status>.FromValue(Status.Archived) };

        // Act
        using var stream = new MemoryStream();
        model.Serialize(stream, original);
        stream.Position = 0;
        var result = (Wrapper)model.Deserialize(stream, null, typeof(Wrapper));

        // Assert
        result.Status.Should().Be(original.Status);
        result.Status.Value.Should().Be(Status.Archived);
    }

    /// <summary>Verifies that an unknown numeric value is preserved across protobuf serialization.</summary>
    [TestMethod]
    public void Roundtrips_UnknownNumber()
    {
        // Arrange
        var model = CreateModel();
        var original = new Wrapper { Status = EvolvableEnum<Status>.FromNumber(999) };

        // Act
        using var stream = new MemoryStream();
        model.Serialize(stream, original);
        stream.Position = 0;
        var result = (Wrapper)model.Deserialize(stream, null, typeof(Wrapper));

        // Assert
        result.Status.IsDefined.Should().BeFalse();
        result.Status.ToNumber().Should().Be(999);
    }

    /// <summary>Verifies that a ulong-backed enum's maximum value round-trips without sign corruption.</summary>
    [TestMethod]
    public void Roundtrips_ULongMaxValue_WithoutSignCorruption()
    {
        // Arrange
        var model = CreateModel();
        var original = new ULongWrapper
        {
            Status = EvolvableEnum<ULongStatus, ulong>.FromValue(ULongStatus.Huge),
        };

        // Act
        using var stream = new MemoryStream();
        model.Serialize(stream, original);
        stream.Position = 0;
        var result = (ULongWrapper)model.Deserialize(stream, null, typeof(ULongWrapper));

        // Assert
        result.Status.Value.Should().Be(ULongStatus.Huge);
        result.Status.ToNumber().Should().Be(ulong.MaxValue);
    }

    /// <summary>
    /// Verifies that serializing an unknown-name-only value (with no numeric representation) fails
    /// explicitly instead of silently corrupting the value, since protobuf has no way to carry a name.
    /// </summary>
    [TestMethod]
    public void Serializing_UnknownName_ShouldThrowExplicitly()
    {
        // Arrange
        var model = CreateModel();
        var original = new Wrapper { Status = EvolvableEnum<Status>.FromName("FutureMember") };

        // Act
        using var stream = new MemoryStream();
        var act = () => model.Serialize(stream, original);

        // Assert
        act.Should().Throw<EvolvableEnumConversionException>();
    }
}
