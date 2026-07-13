// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using MessagePack;

using ProtoBuf;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Discriminator for the polymorphic <see cref="Shape"/> hierarchy.</summary>
public enum ShapeKind
{
    /// <summary>A <see cref="Circle"/>.</summary>
    Circle,

    /// <summary>A <see cref="Square"/>.</summary>
    Square,
}

/// <summary>
/// Polymorphic base carried across the transport wire. Deserialization is driven by the
/// <see cref="Kind"/> discriminator through <see cref="Ark.Tools.SystemTextJson.JsonPolymorphicConverter{TBase, TDiscriminatorEnum}"/>,
/// mirroring the convention used by <c>WebApplicationDemo</c> and the reference project.
/// Protobuf and MessagePack use matching numbered subtype envelopes; JSON keeps its named
/// discriminator because the protobuf envelope is not its native wire representation.
/// </summary>
[JsonConverter(typeof(ShapePolymorphicConverter))]
[ProtoContract]
[ProtoInclude(10, typeof(Circle))]
[ProtoInclude(11, typeof(Square))]
[MessagePackObject]
[Union(10, typeof(Circle))]
[Union(11, typeof(Square))]
public abstract record Shape
{
    /// <summary>Gets the discriminator identifying the concrete shape.</summary>
    [IgnoreMember]
    public abstract ShapeKind Kind { get; }
}

/// <summary>A circle shape.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record Circle : Shape
{
    /// <inheritdoc />
    public override ShapeKind Kind => ShapeKind.Circle;

    /// <summary>Gets the circle radius.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public required double Radius { get; init; }
}

/// <summary>A square shape.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record Square : Shape
{
    /// <inheritdoc />
    public override ShapeKind Kind => ShapeKind.Square;

    /// <summary>Gets the square side length.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public required double Side { get; init; }
}

/// <summary>Response echoing the polymorphic shape and its computed area.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record ShapeDescription
{
    /// <summary>Gets the shape that was described (polymorphic on the wire).</summary>
    [ProtoMember(1)]
    [Key(0)]
    public required Shape Shape { get; init; }

    /// <summary>Gets the computed area of the shape.</summary>
    [ProtoMember(2)]
    [Key(1)]
    public required double Area { get; init; }

    /// <summary>Gets nested metadata containing another polymorphic shape reference.</summary>
    [ProtoMember(3)]
    [Key(2)]
    public required ShapeEnvelope Metadata { get; init; }
}

/// <summary>Nested object carrying a polymorphic shape.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record ShapeEnvelope
{
    /// <summary>Gets the envelope label.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public required string Label { get; init; }

    /// <summary>Gets the nested polymorphic shape.</summary>
    [ProtoMember(2)]
    [Key(1)]
    public required Shape FeaturedShape { get; init; }
}

/// <summary>
/// Pure request carrying a polymorphic <see cref="Shape"/> in its body and returning a polymorphic
/// response, proving polymorphism works on both the request and response wire through a single
/// generated Minimal API endpoint.
/// </summary>
[HttpEndpoint("POST", "/api/v{version}/shapes/describe", AcceptsMessagePack = true)]
[GrpcMethod("DescribeShape")]
[ServiceGroup("Greetings")]
[ProtoContract]
[MessagePackObject]
public sealed record DescribeShapeRequest : IRequest<ShapeDescription>
{
    /// <summary>Gets the shape to describe.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public required Shape Shape { get; init; }
}

/// <summary>
/// System.Text.Json converter mapping the <see cref="ShapeKind"/> discriminator to the concrete
/// <see cref="Shape"/> subtype, built on the shared Ark polymorphic converter.
/// </summary>
internal sealed class ShapePolymorphicConverter : Ark.Tools.SystemTextJson.JsonPolymorphicConverter<Shape, ShapeKind>
{
    public ShapePolymorphicConverter()
        : base(nameof(Shape.Kind))
    {
    }

    protected override Type GetType(ShapeKind discriminatorValue) => discriminatorValue switch
    {
        ShapeKind.Circle => typeof(Circle),
        ShapeKind.Square => typeof(Square),
        _ => throw new NotSupportedException($"Unknown shape kind '{discriminatorValue}'."),
    };
}
