// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

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
/// </summary>
[JsonConverter(typeof(ShapePolymorphicConverter))]
public abstract record Shape
{
    /// <summary>Gets the discriminator identifying the concrete shape.</summary>
    public abstract ShapeKind Kind { get; }
}

/// <summary>A circle shape.</summary>
public sealed record Circle : Shape
{
    /// <inheritdoc />
    public override ShapeKind Kind => ShapeKind.Circle;

    /// <summary>Gets the circle radius.</summary>
    public required double Radius { get; init; }
}

/// <summary>A square shape.</summary>
public sealed record Square : Shape
{
    /// <inheritdoc />
    public override ShapeKind Kind => ShapeKind.Square;

    /// <summary>Gets the square side length.</summary>
    public required double Side { get; init; }
}

/// <summary>Response echoing the polymorphic shape and its computed area.</summary>
public sealed record ShapeDescription
{
    /// <summary>Gets the shape that was described (polymorphic on the wire).</summary>
    public required Shape Shape { get; init; }

    /// <summary>Gets the computed area of the shape.</summary>
    public required double Area { get; init; }
}

/// <summary>
/// Pure request carrying a polymorphic <see cref="Shape"/> in its body and returning a polymorphic
/// response, proving polymorphism works on both the request and response wire through a single
/// generated Minimal API endpoint.
/// </summary>
[HttpEndpoint("POST", "/api/v1/shapes/describe")]
public sealed record DescribeShapeRequest : IRequest<ShapeDescription>
{
    /// <summary>Gets the shape to describe.</summary>
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
