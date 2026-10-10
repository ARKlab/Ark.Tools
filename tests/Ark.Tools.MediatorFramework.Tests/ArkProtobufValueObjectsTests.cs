// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Grpc;

using AwesomeAssertions;

using ProtoBuf;
using ProtoBuf.Meta;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Tests for <see cref="ArkProtobufValueObjects"/>.</summary>
[TestClass]
public class ArkProtobufValueObjectsTests
{
    private readonly record struct OrderId(Guid Value);

    private readonly record struct Quantity(int Value);

    private readonly record struct Copies(int Value);

    [ProtoContract]
    private sealed class Shelf
    {
        [ProtoMember(1)]
        public Copies Copies { get; set; }
    }

    [ProtoContract]
    private sealed class PlainShelf
    {
        [ProtoMember(1)]
        public int Copies { get; set; }
    }

    [ProtoContract]
    private sealed class Order
    {
        [ProtoMember(1)]
        public OrderId Id { get; set; }

        [ProtoMember(2)]
        public OrderId? ParentId { get; set; }

        [ProtoMember(3)]
        public List<Quantity> Quantities { get; set; } = [];
    }

    [ProtoContract]
    [CompatibilityLevel(CompatibilityLevel.Level300)]
    private sealed class PlainOrder
    {
        [ProtoMember(1)]
        public Guid Id { get; set; }

        [ProtoMember(3)]
        public List<int> Quantities { get; set; } = [];
    }

    private static RuntimeTypeModel _model()
    {
        var model = RuntimeTypeModel.Create();
        ArkProtobufValueObjects.Register<OrderId, Guid>(model, static value => new OrderId(value), static value => value.Value);
        ArkProtobufValueObjects.Register<Quantity, int>(model, static value => new Quantity(value), static value => value.Value);
        return model;
    }

    /// <summary>A value object has the wire format of its primitive, a Guid being a Level300 string.</summary>
    [TestMethod]
    public void Serialize_ShouldMatchPrimitiveWireFormat()
    {
        var id = Guid.NewGuid();
        var model = _model();
        using var valueObjects = new MemoryStream();
        using var primitives = new MemoryStream();

        model.Serialize(valueObjects, new Order { Id = new OrderId(id), Quantities = [new Quantity(3)] });
        model.Serialize(primitives, new PlainOrder { Id = id, Quantities = [3] });

        valueObjects.ToArray().Should().Equal(primitives.ToArray());
    }

    /// <summary>Required, nullable and repeated value objects round-trip.</summary>
    [TestMethod]
    public void Deserialize_ShouldRoundTrip()
    {
        var model = _model();
        var order = new Order { Id = new OrderId(Guid.NewGuid()), ParentId = new OrderId(Guid.NewGuid()), Quantities = [new Quantity(1), new Quantity(2)] };
        using var stream = new MemoryStream();

        model.Serialize(stream, order);
        stream.Position = 0;
        var read = model.Deserialize<Order>(stream);

        read.Id.Should().Be(order.Id);
        read.ParentId.Should().Be(order.ParentId);
        read.Quantities.Should().Equal(order.Quantities);
    }

    /// <summary>A later registration, even on another model, does not change the conversions of the first.</summary>
    [TestMethod]
    public void Register_Again_ShouldKeepTheFirstConversions()
    {
        var first = RuntimeTypeModel.Create();
        var second = RuntimeTypeModel.Create();
        ArkProtobufValueObjects.Register<Copies, int>(first, static value => new Copies(value), static value => value.Value);
        ArkProtobufValueObjects.Register<Copies, int>(second, static value => new Copies(value / 2), static value => value.Value * 2);
        using var stream = new MemoryStream();

        first.Serialize(stream, new Shelf { Copies = new Copies(3) });
        stream.Position = 0;

        RuntimeTypeModel.Create().Deserialize<PlainShelf>(stream).Copies.Should().Be(3);
    }

    /// <summary>A primitive without a protobuf scalar is rejected.</summary>
    [TestMethod]
    public void Register_UnsupportedPrimitive_ShouldThrow()
    {
        var act = static () => ArkProtobufValueObjects.Register<Quantity, decimal>(RuntimeTypeModel.Create(), static value => new Quantity((int)value), static value => value.Value);

        act.Should().Throw<NotSupportedException>();
    }
}
