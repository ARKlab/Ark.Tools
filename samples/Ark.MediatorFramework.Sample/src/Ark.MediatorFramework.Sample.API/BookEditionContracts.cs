// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.API.Authorization;

using MessagePack;

using ProtoBuf;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.Sample.API;

/// <summary>Discriminator for the polymorphic Book edition hierarchy.</summary>
public enum BookEditionKind
{
    /// <summary>A printed Book edition.</summary>
    Print,

    /// <summary>A digital Book edition.</summary>
    Digital,
}

/// <summary>Base contract for Book editions carried across transport boundaries.</summary>
[JsonConverter(typeof(BookEditionPolymorphicConverter))]
[ProtoContract]
[ProtoInclude(10, typeof(PrintBookEdition))]
[ProtoInclude(11, typeof(DigitalBookEdition))]
[MessagePackObject]
[Union(10, typeof(PrintBookEdition))]
[Union(11, typeof(DigitalBookEdition))]
public abstract record BookEdition
{
    /// <summary>Gets the discriminator identifying the concrete edition.</summary>
    [IgnoreMember]
    public abstract BookEditionKind Kind { get; }
}

/// <summary>Describes a printed Book edition.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record PrintBookEdition : BookEdition
{
    /// <inheritdoc />
    public override BookEditionKind Kind => BookEditionKind.Print;

    /// <summary>Gets the print format.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public string Format { get; init; } = string.Empty;

    /// <summary>Gets the number of pages.</summary>
    [ProtoMember(2)]
    [Key(1)]
    public int PageCount { get; init; }
}

/// <summary>Describes a digital Book edition.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record DigitalBookEdition : BookEdition
{
    /// <inheritdoc />
    public override BookEditionKind Kind => BookEditionKind.Digital;

    /// <summary>Gets the digital file format.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public string Format { get; init; } = string.Empty;

    /// <summary>Gets the file size in bytes.</summary>
    [ProtoMember(2)]
    [Key(1)]
    public long SizeBytes { get; init; }
}

/// <summary>Response describing a concrete Book edition.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record BookEditionDescription
{
    /// <summary>Gets the described edition.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public required BookEdition Edition { get; init; }

    /// <summary>Gets the human-readable edition description.</summary>
    [ProtoMember(2)]
    [Key(1)]
    public required string Description { get; init; }
}

/// <summary>Describes a polymorphic Book edition.</summary>
[HttpEndpoint("POST", "/api/v{version}/books/editions/describe", AcceptsMessagePack = true)]
[GrpcMethod("DescribeBookEdition")]
[GrpcService("Books")]
[RequireScopePolicy(ApplicationScopes.BookRead)]
[ProtoContract]
[MessagePackObject]
public sealed record DescribeBookEditionRequest : IRequest<DescribeBookEditionRequest, BookEditionDescription>
{
    /// <summary>Gets the edition to describe.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public required BookEdition Edition { get; init; }
}

/// <summary>System.Text.Json converter for the Book edition discriminator.</summary>
public sealed class BookEditionPolymorphicConverter : Tools.SystemTextJson.JsonPolymorphicConverter<BookEdition, BookEditionKind>
{
    /// <summary>Initializes a new instance of the <see cref="BookEditionPolymorphicConverter"/> class.</summary>
    public BookEditionPolymorphicConverter()
        : base(nameof(BookEdition.Kind))
    {
    }

    /// <inheritdoc />
    protected override Type GetType(BookEditionKind discriminatorValue) => discriminatorValue switch
    {
        BookEditionKind.Print => typeof(PrintBookEdition),
        BookEditionKind.Digital => typeof(DigitalBookEdition),
        _ => throw new NotSupportedException($"Unknown Book edition kind '{discriminatorValue}'."),
    };
}
