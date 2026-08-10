// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Outbox.Rebus;

using NodaTime;

using Rebus.Bus;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Creates books through the application contract.</summary>
public sealed class CreateBookHandler : IRequestHandler<Book_CreateRequest.V1, Book.V1.Output>
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
    public async Task<Book.V1.Output> ExecuteAsync(Book_CreateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var book = CreateResponse(Guid.NewGuid(), request.Data.Title, request.Data.Author, request.Data.Genre, request.Data.ISBN);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.WriteAuditAsync(CreateAudit(book.Id, typeof(Book_CreateRequest).Name + "." + typeof(Book_CreateRequest.V1).Name), ctk).ConfigureAwait(false);
        await context.SaveBookAsync(book, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }

    internal static Book.V1.Output CreateResponse(
        Guid id,
        string title,
        string author,
        Ark.Tools.Core.EvolvableEnum<Book.V1.Genre> genre,
        string? isbn)
    {
        return new Book.V1.Output
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
            EntityType = nameof(Book.V1.Output),
            Identifier = id.ToString("D"),
            Operation = operation,
            Timestamp = _clock.GetCurrentInstant(),
        };
    }
}

/// <summary>Updates books through the application contract.</summary>
public sealed class UpdateBookHandler : IRequestHandler<Book_UpdateRequest.V1, Book.V1.Output>
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
    public async Task<Book.V1.Output> ExecuteAsync(Book_UpdateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var current = await context.ReadBookAsync(request.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book '{request.Id}' was not found.");
        var book = current with
        {
            Title = request.Data.Title,
            Author = request.Data.Author,
            Genre = request.Data.Genre,
            Description = $"Book updated: {request.Data.Title} by {request.Data.Author}",
        };
        if (!await context.UpdateBookAsync(book, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{book.Id}' was not found.");
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(Book.V1.Output),
            Identifier = book.Id.ToString("D"),
            Operation = typeof(Book_UpdateRequest).Name + "." + typeof(Book_UpdateRequest.V1).Name,
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }
}

/// <summary>Deletes books through the application contract.</summary>
public sealed class DeleteBookHandler : IRequestHandler<Book_DeleteRequest.V1, bool>
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
    public async Task<bool> ExecuteAsync(Book_DeleteRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (!await context.DeleteBookAsync(request.Id, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{request.Id}' was not found.");
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(Book.V1.Output),
            Identifier = request.Id.ToString("D"),
            Operation = typeof(Book_DeleteRequest).Name + "." + typeof(Book_DeleteRequest.V1).Name,
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
    private readonly ISampleDataContextFactory _factory;
    private readonly IBus _bus;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="CreateBookPrintProcessHandler"/> class.</summary>
    public CreateBookPrintProcessHandler(
        ISampleDataContextFactory factory,
        IBus bus,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock)
    {
        _factory = factory;
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
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (await context.ReadBookAsync(request.BookId, ctk).ConfigureAwait(false) is null)
            throw new EntityNotFoundException($"Book '{request.BookId}' was not found.");

        var process = new BookPrintProcessResponse
        {
            Id = Guid.NewGuid(),
            BookId = request.BookId,
            Status = BookPrintProcessStatus.Pending,
            ShouldFail = request.ShouldFail,
        };
        if (!await context.TrySaveBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
            throw new BusinessRuleViolationException(new BookPrintingProcessAlreadyRunningViolation(request.BookId));
        await context.WriteAuditAsync(CreateAudit(process.Id, nameof(CreateBookPrintProcessRequest)), ctk).ConfigureAwait(false);
        using var scope = _bus.Enlist(context.OutboxContext);
        await _bus.Send(new ProcessBookPrintProcessRequest { Id = process.Id }).ConfigureAwait(false);
        await scope.CompleteAsync().ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
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
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;
    private readonly IPrintCompletedNotificationService _printCompletedNotificationService;

    /// <summary>Initializes a new instance of the <see cref="ProcessBookPrintProcessHandler"/> class.</summary>
    public ProcessBookPrintProcessHandler(
        ISampleDataContextFactory factory,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock,
        IPrintCompletedNotificationService printCompletedNotificationService)
    {
        _factory = factory;
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
        await using var readContext = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var process = await readContext.ReadBookPrintProcessAsync(request.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book print process '{request.Id}' was not found.");
        await readContext.CommitAsync(ctk).ConfigureAwait(false);
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
            process = await PersistAsync(process, ctk).ConfigureAwait(false);
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
        process = await PersistAsync(process, ctk).ConfigureAwait(false);
        if (process.Status == BookPrintProcessStatus.Completed)
            await _printCompletedNotificationService.NotifyAsync(process, ctk).ConfigureAwait(false);
        return process;
    }

    private async Task<BookPrintProcessResponse> PersistAsync(
        BookPrintProcessResponse process,
        CancellationToken ctk)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.WriteAuditAsync(CreateAudit(process.Id), ctk).ConfigureAwait(false);
        if (!await context.UpdateBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book print process '{process.Id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
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
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetBookPrintProcessHandler"/> class.</summary>
    public GetBookPrintProcessHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        GetBookPrintProcessQuery query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var process = await context.ReadBookPrintProcessAsync(query.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book print process '{query.Id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process;
    }
}

/// <summary>Reads books through the application contract.</summary>
public sealed class GetBookHandler : IQueryHandler<Book_GetQuery.V1, Book.V1.Output>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetBookHandler"/> class.</summary>
    public GetBookHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<Book.V1.Output> ExecuteAsync(Book_GetQuery.V1 query, CancellationToken ctk = default)
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
public sealed class SearchBooksHandler : IQueryHandler<Book_SearchQuery.V1, Book.V1.Page>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="SearchBooksHandler"/> class.</summary>
    public SearchBooksHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<Book.V1.Page> ExecuteAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var result = await context.ReadBooksAsync(query, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }
}
