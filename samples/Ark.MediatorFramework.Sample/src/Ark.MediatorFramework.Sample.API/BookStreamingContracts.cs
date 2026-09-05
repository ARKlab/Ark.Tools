// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.API.Authorization;
using Ark.Tools.Solid;

using MessagePack;

using ProtoBuf;

namespace Ark.MediatorFramework.Sample.API;

/// <summary>Item yielded by the incremental Book stream.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record BookStreamItem
{
    /// <summary>Gets the zero-based item index.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public int Index { get; init; }

    /// <summary>Gets the streamed Book title.</summary>
    [ProtoMember(2)]
    [Key(1)]
    public required string Title { get; init; }

    /// <summary>Gets the streamed Book author.</summary>
    [ProtoMember(3)]
    [Key(2)]
    public required string Author { get; init; }
}

/// <summary>Streams Book items without buffering the complete result.</summary>
public static class StreamBooksQuery
{
    /// <summary>Version one of the Book streaming query.</summary>
    [HttpEndpoint("GET", "/api/v{version}/books/stream", AcceptsMessagePack = true)]
    [Sse]
    [GrpcMethod("StreamBooks")]
    [GrpcService("Books")]
    [RequireScopePolicy(ApplicationScopes.BookRead)]
    [ProtoContract]
    [MessagePackObject]
    public sealed record V1 : IQuery<V1, IAsyncEnumerable<BookStreamItem>>
    {
        /// <summary>Gets the number of items to yield.</summary>
        [HttpQuery]
        [ProtoMember(1)]
        [Key(0)]
        public int Count { get; init; }

        /// <summary>Gets the delay between yielded items in milliseconds.</summary>
        [HttpQuery]
        [ProtoMember(2)]
        [Key(1)]
        public int DelayMilliseconds { get; init; }
    }
}
