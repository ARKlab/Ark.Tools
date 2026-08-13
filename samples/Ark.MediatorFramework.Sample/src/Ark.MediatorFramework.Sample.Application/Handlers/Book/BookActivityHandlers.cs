// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

using NodaTime;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Creates book reviews through the application contract.</summary>
public sealed class CreateBookReviewHandler : IRequestHandler<CreateBookReviewRequest, BookReview>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="CreateBookReviewHandler"/> class.</summary>
    public CreateBookReviewHandler(
        ISampleDataContextFactory factory,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<BookReview> ExecuteAsync(
        CreateBookReviewRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = _user.GetUserId() ?? "anonymous";
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        _ = await context.ReadBookAsync(request.BookId, ctk: ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book '{request.BookId}' was not found.");
        var review = new BookReview
        {
            Id = Guid.NewGuid(),
            BookId = request.BookId,
            UserId = userId,
            Rating = request.Rating,
            Text = request.Text,
            CreatedAt = _clock.GetCurrentInstant(),
        };
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EntityType = nameof(BookReview),
            Identifier = review.Id.ToString("D"),
            Operation = nameof(CreateBookReviewRequest),
            Timestamp = review.CreatedAt,
        }, ctk).ConfigureAwait(false);
        await context.SaveBookReviewAsync(review, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return review;
    }
}

/// <summary>Lists reviews for a book through the application contract.</summary>
public sealed class ListBookReviewsHandler : IQueryHandler<ListBookReviewsQuery, IReadOnlyList<BookReview>>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="ListBookReviewsHandler"/> class.</summary>
    public ListBookReviewsHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookReview>> ExecuteAsync(
        ListBookReviewsQuery query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        _ = await context.ReadBookAsync(query.BookId, ctk: ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book '{query.BookId}' was not found.");
        var reviews = await context.ReadBookReviewsAsync(query.BookId, query.Skip, query.Limit, ctk)
            .ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return reviews;
    }
}

/// <summary>Records reading activity through the application contract.</summary>
public sealed class RecordReadingActivityHandler :
    IRequestHandler<RecordReadingActivityRequest, ReadingActivity>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="RecordReadingActivityHandler"/> class.</summary>
    public RecordReadingActivityHandler(
        ISampleDataContextFactory factory,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ReadingActivity> ExecuteAsync(
        RecordReadingActivityRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var activity = new ReadingActivity
        {
            Id = Guid.NewGuid(),
            BookId = request.BookId,
            UserId = _user.GetUserId() ?? "anonymous",
            Kind = request.Kind,
            Progress = request.Progress,
            OccurredAt = _clock.GetCurrentInstant(),
        };
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        _ = await context.ReadBookAsync(request.BookId, ctk: ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book '{request.BookId}' was not found.");
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = activity.UserId,
            EntityType = nameof(ReadingActivity),
            Identifier = activity.Id.ToString("D"),
            Operation = nameof(RecordReadingActivityRequest),
            Timestamp = activity.OccurredAt,
        }, ctk).ConfigureAwait(false);
        await context.SaveReadingActivityAsync(activity, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return activity;
    }
}

/// <summary>Reads recent activity for a book through the application contract.</summary>
public sealed class GetReadingActivityHandler :
    IQueryHandler<GetReadingActivityQuery, IReadOnlyList<ReadingActivity>>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;

    /// <summary>Initializes a new instance of the <see cref="GetReadingActivityHandler"/> class.</summary>
    public GetReadingActivityHandler(
        ISampleDataContextFactory factory,
        IContextProvider<ClaimsPrincipal> user)
    {
        _factory = factory;
        _user = user;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReadingActivity>> ExecuteAsync(
        GetReadingActivityQuery query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        _ = await context.ReadBookAsync(query.BookId, ctk: ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book '{query.BookId}' was not found.");
        var activities = await context.ReadReadingActivityAsync(
            query.BookId,
            _user.GetUserId() ?? "anonymous",
            query.Limit,
            ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return activities;
    }
}
