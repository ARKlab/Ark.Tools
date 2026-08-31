// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Runtime.CompilerServices;

using Google.Protobuf;
using Google.Protobuf.Reflection;

using MessagePack;

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Publishes a MessagePack book-print event.</summary>
[Event(Name = "books/book-print.messagepack")]
[MessagePackObject]
public sealed record BookPrintMessagePackEvent : ICommand<BookPrintMessagePackEvent>
{
    /// <summary>Gets the printed book identifier.</summary>
    [Key(0)]
    public Guid BookId { get; init; }
}

/// <summary>
/// Publishes a minimal protobuf-shaped book-print message for topology validation.
/// Production contracts should be generated from a <c>.proto</c> schema.
/// </summary>
[Message(Name = "books/book-print.protobuf")]
public sealed class BookPrintProtobufMessage : IMessage<BookPrintProtobufMessage>
{
    private static readonly MessageParser<BookPrintProtobufMessage> _parser = new(() => new());

    /// <summary>Gets the generated protobuf parser.</summary>
    public static MessageParser<BookPrintProtobufMessage> Parser => _parser;

    /// <inheritdoc />
    public MessageDescriptor Descriptor => throw new NotSupportedException(
        "This sample contract exists only to exercise compile-time protobuf shape validation.");

    /// <inheritdoc />
    public BookPrintProtobufMessage Clone()
    {
        return new BookPrintProtobufMessage();
    }

    /// <inheritdoc />
    public void MergeFrom(BookPrintProtobufMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
    }

    /// <inheritdoc />
    public void MergeFrom(CodedInputStream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        while (input.ReadTag() is not 0)
            input.SkipLastField();
    }

    /// <inheritdoc />
    public void WriteTo(CodedOutputStream output)
    {
        ArgumentNullException.ThrowIfNull(output);
    }

    /// <inheritdoc />
    public int CalculateSize()
    {
        return 0;
    }

    /// <inheritdoc />
    public bool Equals(BookPrintProtobufMessage? other)
    {
        return ReferenceEquals(this, other);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as BookPrintProtobufMessage);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return RuntimeHelpers.GetHashCode(this);
    }
}
