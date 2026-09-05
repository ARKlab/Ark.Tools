// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.API.Authorization;

using Ark.Tools.Core;
using Ark.Tools.Solid;

using NodaTime;

namespace Ark.MediatorFramework.Sample.API;

/// <summary>Names the reading activity recorded for a book.</summary>
public enum ReadingActivityKind
{
    /// <summary>No activity kind has been selected.</summary>
    NOT_SET = 0,

    /// <summary>The reader started the book.</summary>
    Started,

    /// <summary>The reader advanced through the book.</summary>
    Progressed,

    /// <summary>The reader finished the book.</summary>
    Finished,
}

/// <summary>Represents one reading activity event.</summary>
public sealed record ReadingActivity
{
    /// <summary>Gets the activity identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the book identifier.</summary>
    public required Guid BookId { get; init; }

    /// <summary>Gets the authenticated reader identifier.</summary>
    public required string UserId { get; init; }

    /// <summary>Gets the activity kind.</summary>
    public required EvolvableEnum<ReadingActivityKind> Kind { get; init; }

    /// <summary>Gets the percentage read at the time of the activity.</summary>
    public required int Progress { get; init; }

    /// <summary>Gets the activity timestamp.</summary>
    public required Instant OccurredAt { get; init; }
}

/// <summary>Records reading activity for a book.</summary>
public static class RecordReadingActivityRequest
{
    /// <summary>Version one of the reading-activity recording request.</summary>
    [HttpEndpoint("POST", "/api/v{version}/books/{bookId}/reading-activity")]
    [RequireScopePolicy(ApplicationScopes.BookActivityWrite)]
    public sealed record V1 : IRequest<V1, ReadingActivity>
    {
        /// <summary>Gets the book identifier.</summary>
        [HttpRoute]
        public Guid BookId { get; init; }

        /// <summary>Gets the activity kind.</summary>
        public EvolvableEnum<ReadingActivityKind> Kind { get; init; }

        /// <summary>Gets the percentage read at the time of the activity.</summary>
        public int Progress { get; init; }
    }
}

/// <summary>Reads recent activity for a book and the current reader.</summary>
public static class GetReadingActivityQuery
{
    /// <summary>Version one of the recent reading-activity query.</summary>
    [HttpEndpoint("GET", "/api/v{version}/books/{bookId}/reading-activity")]
    [RequireScopePolicy(ApplicationScopes.BookActivityRead)]
    public sealed record V1 : IQuery<V1, IReadOnlyList<ReadingActivity>>
    {
        /// <summary>Gets the book identifier.</summary>
        [HttpRoute]
        public Guid BookId { get; init; }

        /// <summary>Gets the maximum number of activity events to return.</summary>
        [HttpQuery]
        public int Limit { get; init; } = 25;
    }
}
