// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;

using NodaTime;

using Rebus.Bus;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Creates books through the application contract.</summary>
public sealed class CreateBookHandler : IRequestHandler<CreateBookRequest, BookResponse>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="CreateBookHandler"/> class.</summary>
    public CreateBookHandler(ISampleDataContextFactory factory, IContextProvider<ClaimsPrincipal> user, IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<BookResponse> ExecuteAsync(CreateBookRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var book = CreateResponse(Guid.NewGuid(), request.Title, request.Author, request.Genre, request.ISBN);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.WriteAuditAsync(CreateAudit(book.Id, nameof(CreateBookRequest)), ctk).ConfigureAwait(false);
        await context.SaveBookAsync(book, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }

    internal static BookResponse CreateResponse(
        Guid id,
        string title,
        string author,
        Ark.Tools.Core.EvolvableEnum<BookGenre> genre,
        string? isbn)
    {
        return new BookResponse
        {
            Id = id,
            Title = title,
            Author = author,
            Genre = genre,
            ISBN = isbn,
            Description = $"Book created: {title} by {author}",
        };
    }

    private AuditEntry CreateAudit(Guid id, string operation)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookResponse),
            Identifier = id.ToString("D"),
            Operation = operation,
            Timestamp = _clock.GetCurrentInstant(),
        };
    }
}

/// <summary>Updates books through the application contract.</summary>
public sealed class UpdateBookHandler : IRequestHandler<UpdateBookRequest, BookResponse>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="UpdateBookHandler"/> class.</summary>
    public UpdateBookHandler(ISampleDataContextFactory factory, IContextProvider<ClaimsPrincipal> user, IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<BookResponse> ExecuteAsync(UpdateBookRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var book = CreateBookHandler.CreateResponse(request.Id, request.Title, request.Author, request.Genre, request.ISBN) with
        {
            Description = $"Book updated: {request.Title} by {request.Author}",
        };
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (!await context.UpdateBookAsync(book, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{book.Id}' was not found.");
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookResponse),
            Identifier = book.Id.ToString("D"),
            Operation = nameof(UpdateBookRequest),
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }
}

/// <summary>Deletes books through the application contract.</summary>
public sealed class DeleteBookHandler : IRequestHandler<DeleteBookRequest, bool>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="DeleteBookHandler"/> class.</summary>
    public DeleteBookHandler(ISampleDataContextFactory factory, IContextProvider<ClaimsPrincipal> user, IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(DeleteBookRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (!await context.DeleteBookAsync(request.Id, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{request.Id}' was not found.");
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookResponse),
            Identifier = request.Id.ToString("D"),
            Operation = nameof(DeleteBookRequest),
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return true;
    }
}

/// <summary>Starts a background print process for a persisted book.</summary>
public sealed class CreateBookPrintProcessHandler :
    IRequestHandler<CreateBookPrintProcessRequest, BookPrintProcessResponse>
{
    private readonly IBookStore _store;
    private readonly IBus _bus;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="CreateBookPrintProcessHandler"/> class.</summary>
    public CreateBookPrintProcessHandler(
        IBookStore store,
        IBus bus,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock)
    {
        _store = store;
        _bus = bus;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        CreateBookPrintProcessRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _store.GetAsync(request.BookId, ctk).ConfigureAwait(false);

        var process = new BookPrintProcessResponse
        {
            Id = Guid.NewGuid(),
            BookId = request.BookId,
            Status = BookPrintProcessStatus.Pending,
            ShouldFail = request.ShouldFail,
        };
        if (!await _store.TryCreateAndQueuePrintProcessAsync(
                process,
                CreateAudit(process.Id, nameof(CreateBookPrintProcessRequest)),
                _bus,
                ctk).ConfigureAwait(false))
            throw new BusinessRuleViolationException(new BookPrintingProcessAlreadyRunningViolation(request.BookId));
        return process;
    }

    private AuditEntry CreateAudit(Guid id, string operation)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookPrintProcessResponse),
            Identifier = id.ToString("D"),
            Operation = operation,
            Timestamp = _clock.GetCurrentInstant(),
        };
    }
}

/// <summary>Completes a queued book print process through Rebus.</summary>
public sealed class ProcessBookPrintProcessHandler :
    IRequestHandler<ProcessBookPrintProcessRequest, BookPrintProcessResponse>
{
    private readonly IBookStore _store;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;
    private readonly IPrintCompletedNotificationService _printCompletedNotificationService;

    /// <summary>Initializes a new instance of the <see cref="ProcessBookPrintProcessHandler"/> class.</summary>
    public ProcessBookPrintProcessHandler(
        IBookStore store,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock,
        IPrintCompletedNotificationService printCompletedNotificationService)
    {
        _store = store;
        _user = user;
        _clock = clock;
        _printCompletedNotificationService = printCompletedNotificationService;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        ProcessBookPrintProcessRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var process = await _store.GetPrintProcessAsync(request.Id, ctk).ConfigureAwait(false);
        if (process.Status == BookPrintProcessStatus.Completed)
        {
            await _printCompletedNotificationService.NotifyAsync(process, ctk).ConfigureAwait(false);
            return process;
        }
        if (process.Status != BookPrintProcessStatus.Pending && process.Status != BookPrintProcessStatus.Running)
            return process;

        if (process.Status == BookPrintProcessStatus.Pending)
        {
            process = process with
            {
                Progress = 0.5,
                Status = BookPrintProcessStatus.Running,
            };
            await _store.UpdatePrintProcessAsync(process, CreateAudit(process.Id), ctk).ConfigureAwait(false);
        }

        process = process.ShouldFail
            ? process with
            {
                Status = BookPrintProcessStatus.Error,
                ErrorMessage = "The test book print process failed.",
            }
            : process with
            {
                Progress = 1,
                Status = BookPrintProcessStatus.Completed,
            };
        process = await _store.UpdatePrintProcessAsync(process, CreateAudit(process.Id), ctk).ConfigureAwait(false);
        if (process.Status == BookPrintProcessStatus.Completed)
            await _printCompletedNotificationService.NotifyAsync(process, ctk).ConfigureAwait(false);
        return process;
    }

    private AuditEntry CreateAudit(Guid id)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookPrintProcessResponse),
            Identifier = id.ToString("D"),
            Operation = nameof(ProcessBookPrintProcessRequest),
            Timestamp = _clock.GetCurrentInstant(),
        };
    }
}

/// <summary>Reads a book print process.</summary>
public sealed class GetBookPrintProcessHandler :
    IQueryHandler<GetBookPrintProcessQuery, BookPrintProcessResponse>
{
    private readonly IBookStore _store;

    /// <summary>Initializes a new instance of the <see cref="GetBookPrintProcessHandler"/> class.</summary>
    public GetBookPrintProcessHandler(IBookStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        GetBookPrintProcessQuery query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _store.GetPrintProcessAsync(query.Id, ctk).ConfigureAwait(false);
    }
}

/// <summary>Reads books through the application contract.</summary>
public sealed class GetBookHandler : IQueryHandler<GetBookQuery, BookResponse>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetBookHandler"/> class.</summary>
    public GetBookHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<BookResponse> ExecuteAsync(GetBookQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var book = await context.ReadBookAsync(query.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book '{query.Id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }
}

/// <summary>Searches books through the application contract.</summary>
public sealed class SearchBooksHandler : IQueryHandler<SearchBooksQuery, BookPage>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="SearchBooksHandler"/> class.</summary>
    public SearchBooksHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<BookPage> ExecuteAsync(SearchBooksQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var result = await context.ReadBooksAsync(query, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }
}
