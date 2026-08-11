// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;

using FluentValidation;
using FluentValidation.Results;

using Rebus.Bus;

using System.Collections.Concurrent;
using System.Security.Claims;

using NodaTime;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Handles the synchronous refresh command.</summary>
public sealed class RefreshGreetingHandler : ICommandHandler<RefreshGreetingCommand>
{
    /// <inheritdoc />
    public async Task ExecuteAsync(RefreshGreetingCommand command, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>Creates greetings through the versioned application contract.</summary>
public sealed class CreateGreetingHandler : IRequestHandler<Greeting_CreateRequest.V1, Greeting.V1.Output>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="CreateGreetingHandler"/> class.</summary>
    public CreateGreetingHandler(ISampleDataContextFactory factory, IContextProvider<ClaimsPrincipal> user, IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Greeting.V1.Output> ExecuteAsync(Greeting_CreateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if ((await context.ReadAllAsync(ctk).ConfigureAwait(false)).Any(g => g.Message.Contains($"Hello, {request.Data.Name}!", StringComparison.Ordinal)))
            throw new BusinessRuleViolationException(new GreetingAlreadyExistsViolation(request.Data.Name));

        var auditId = Guid.NewGuid();
        var response = new Greeting.V1.Output
        {
            Id = Guid.NewGuid(),
            AuditId = auditId,
            Message = $"Hello, {request.Data.Name}! (by {_user.GetUserId() ?? "anonymous"})",
            Date = request.Data.Date,
            DateTime = request.Data.DateTime,
            OffsetDateTime = request.Data.OffsetDateTime,
            Period = request.Data.Period,
            ETag = Convert.ToBase64String(BitConverter.GetBytes(1L)),
        };

        await context.WriteAuditAsync(new AuditEntry
        {
            Id = auditId,
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(GreetingResponse),
            Identifier = response.Id.ToString("D"),
            Operation = $"{typeof(Greeting_CreateRequest).Name}.{typeof(Greeting_CreateRequest.V1).Name}",
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.SaveAsync(ToLegacy(response), ctk).ConfigureAwait(false);
        var persisted = await context.ReadAsync(response.Id, ctk).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The greeting was not persisted.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return ToOutput(persisted);
    }

    internal static GreetingResponse ToLegacy(Greeting.V1.Output greeting)
    {
        return new GreetingResponse
        {
            Id = greeting.Id,
            Message = greeting.Message,
            Date = greeting.Date,
            DateTime = greeting.DateTime,
            OffsetDateTime = greeting.OffsetDateTime,
            Period = greeting.Period,
            AuditId = greeting.AuditId,
            ETag = greeting.ETag,
        };
    }

    internal static Greeting.V1.Output ToOutput(GreetingResponse greeting)
    {
        return new Greeting.V1.Output
        {
            Id = greeting.Id,
            Message = greeting.Message,
            Date = greeting.Date,
            DateTime = greeting.DateTime,
            OffsetDateTime = greeting.OffsetDateTime,
            Period = greeting.Period,
            AuditId = greeting.AuditId,
            ETag = greeting.ETag,
        };
    }
}

/// <summary>Updates a greeting after validating its opaque concurrency token.</summary>
public sealed class UpdateGreetingMessageHandler : IRequestHandler<Greeting_UpdateRequest.V1, Greeting.V1.Output>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="UpdateGreetingMessageHandler"/> class.</summary>
    public UpdateGreetingMessageHandler(ISampleDataContextFactory factory, IContextProvider<ClaimsPrincipal> user, IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Greeting.V1.Output> ExecuteAsync(Greeting_UpdateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var audit = new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(GreetingResponse),
            Identifier = request.Id.ToString("D"),
            Operation = $"{typeof(Greeting_UpdateRequest).Name}.{typeof(Greeting_UpdateRequest.V1).Name}",
            Timestamp = _clock.GetCurrentInstant(),
        };
        var updated = await context.UpdateAsync(request.Id, request.Data.Message, request.ETag ?? string.Empty, audit.Id, ctk).ConfigureAwait(false)
            ?? throw new Tools.Core.EntityTag.EntityTagMismatchException("The greeting ETag did not match.");
        await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return CreateGreetingHandler.ToOutput(updated);
    }
}

/// <summary>Pure handler for <see cref="ComposeGreetingRequest"/> that publishes work to Rebus.</summary>
public sealed class ComposeGreetingHandler : IRequestHandler<ComposeGreetingRequest, ComposeGreetingResponse>
{
    private readonly IBus _bus;

    /// <summary>Initializes a new instance of the <see cref="ComposeGreetingHandler"/> class.</summary>
    public ComposeGreetingHandler(IBus bus)
    {
        _bus = bus;
    }

    /// <inheritdoc />
    public async Task<ComposeGreetingResponse> ExecuteAsync(ComposeGreetingRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);

        if (string.IsNullOrWhiteSpace(Request.Name))
            throw new ValidationException([new ValidationFailure(nameof(Request.Name), "Name must not be empty.")]);

        var id = Guid.NewGuid();
        await _bus.Send(new CompleteGreetingCompositionRequest
        {
            Id = id,
            Name = Request.Name,
            FailuresBeforeSuccess = Request.FailuresBeforeSuccess,
        }).ConfigureAwait(false);

        return new ComposeGreetingResponse
        {
            Id = id,
            Status = "queued",
        };
    }
}

/// <summary>Pure handler for <see cref="CompleteGreetingCompositionRequest"/> that completes the workflow.</summary>
public sealed class CompleteGreetingCompositionHandler : IRequestHandler<CompleteGreetingCompositionRequest, GreetingResponse>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;
    private readonly GreetingCompositionRetryTracker _retryTracker;

    /// <summary>Initializes a new instance of the <see cref="CompleteGreetingCompositionHandler"/> class.</summary>
    public CompleteGreetingCompositionHandler(
        ISampleDataContextFactory factory,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock,
        GreetingCompositionRetryTracker retryTracker)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
        _retryTracker = retryTracker;
    }

    /// <inheritdoc />
    public async Task<GreetingResponse> ExecuteAsync(CompleteGreetingCompositionRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);

        if (_retryTracker.RecordAttempt(Request.Id) <= Request.FailuresBeforeSuccess)
            throw new InvalidOperationException($"Greeting composition '{Request.Id}' failed transiently.");

        var auditId = Guid.NewGuid();
        var response = new GreetingResponse
        {
            Id = Request.Id,
            AuditId = auditId,
            Message = $"Hello, {Request.Name}! (async)",
            ETag = Convert.ToBase64String(BitConverter.GetBytes(1L)),
        };

        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = auditId,
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(GreetingResponse),
            Identifier = response.Id.ToString("D"),
            Operation = nameof(CompleteGreetingCompositionRequest),
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.SaveAsync(response, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return response;
    }

}

/// <summary>Tracks deterministic transient failures for one composition workflow.</summary>
public sealed class GreetingCompositionRetryTracker
{
    private readonly ConcurrentDictionary<Guid, int> _attempts = new();

    /// <summary>Records an attempt and returns its one-based ordinal.</summary>
    /// <param name="id">The composition workflow identifier.</param>
    /// <returns>The one-based attempt number.</returns>
    public int RecordAttempt(Guid id)
    {
        return _attempts.AddOrUpdate(id, 1, static (_, attempts) => attempts + 1);
    }
}

/// <summary>Consumes greeting-created notifications after their transaction commits.</summary>
public sealed class GreetingCreatedHandler : ICommandHandler<GreetingCreatedNotification>
{
    /// <inheritdoc />
    public async Task ExecuteAsync(GreetingCreatedNotification command, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>Handles paged reads of the persisted audit trail.</summary>
public sealed class GetAuditsHandler : IQueryHandler<GetAuditsQuery, PagedResult<AuditRecord>>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetAuditsHandler"/> class.</summary>
    public GetAuditsHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditRecord>> ExecuteAsync(GetAuditsQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var result = await context.ReadAuditsAsync(query, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }
}

/// <summary>Handles paged reads of greetings.</summary>
public sealed class SearchGreetingsHandler : IQueryHandler<SearchGreetingsQuery, GreetingPage>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="SearchGreetingsHandler"/> class.</summary>
    public SearchGreetingsHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<GreetingPage> ExecuteAsync(SearchGreetingsQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var result = await context.ReadGreetingsAsync(query, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }
}

/// <summary>Produces greeting items incrementally for HTTP JSON and gRPC streaming.</summary>
public sealed class GetGreetingsStreamHandler : IQueryHandler<GetGreetingsStreamQuery, IAsyncEnumerable<GreetingStreamItem>>
{
    /// <inheritdoc />
    public async Task<IAsyncEnumerable<GreetingStreamItem>> ExecuteAsync(
        GetGreetingsStreamQuery query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Count < 0)
            throw new ArgumentOutOfRangeException(nameof(query), query.Count, "Count must not be negative.");
        if (query.DelayMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(query), query.DelayMilliseconds, "DelayMilliseconds must not be negative.");

        await Task.CompletedTask.ConfigureAwait(false);
        return StreamAsync(query, ctk);
    }

    private static async IAsyncEnumerable<GreetingStreamItem> StreamAsync(
        GetGreetingsStreamQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ctk)
    {
        for (var index = 0; index < query.Count; index++)
        {
            ctk.ThrowIfCancellationRequested();
            yield return new GreetingStreamItem
            {
                Index = index,
                Message = $"Hello, stream item {index}!",
            };

            if (index + 1 < query.Count)
                await Task.Delay(query.DelayMilliseconds, ctk).ConfigureAwait(false);
        }
    }
}

/// <summary>Pure handler for <see cref="GetGreetingQuery"/> — no transport types.</summary>
public sealed class GetGreetingHandler : IQueryHandler<GetGreetingQuery, GreetingResponse>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetGreetingHandler"/> class.</summary>
    public GetGreetingHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<GreetingResponse> ExecuteAsync(GetGreetingQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var greeting = await context.ReadAsync(query.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Greeting '{query.Id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return greeting;
    }
}

/// <summary>Pure handler for <see cref="GetGreetingV2Query"/> — no transport types.</summary>
public sealed class GetGreetingV2Handler : IQueryHandler<GetGreetingV2Query, GreetingResponseV2>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetGreetingV2Handler"/> class.</summary>
    public GetGreetingV2Handler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<GreetingResponseV2> ExecuteAsync(GetGreetingV2Query query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var greeting = await context.ReadAsync(query.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Greeting '{query.Id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return new GreetingResponseV2
        {
            Id = greeting.Id,
            Message = greeting.Message,
            MessageLength = greeting.Message.Length,
        };
    }
}

/// <summary>Pure handler for <see cref="UpdateGreetingRequest"/>.</summary>
public sealed class UpdateGreetingEnvelopeHandler : IRequestHandler<UpdateGreetingRequest, EnvelopeBindingResponse>
{
    /// <inheritdoc />
    public async Task<EnvelopeBindingResponse> ExecuteAsync(UpdateGreetingRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);
        await Task.CompletedTask.ConfigureAwait(false);
        return new EnvelopeBindingResponse
        {
            Id = Request.Id,
            Audit = Request.Audit,
            Message = Request.Body.Message,
        };
    }
}

/// <summary>Pure handler describing a polymorphic <see cref="Shape"/> — no transport types.</summary>
public sealed class DescribeShapeHandler : IRequestHandler<DescribeShapeRequest, ShapeDescription>
{
    /// <inheritdoc />
    public Task<ShapeDescription> ExecuteAsync(DescribeShapeRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);

        var area = Request.Shape switch
        {
            Circle circle => Math.PI * circle.Radius * circle.Radius,
            Square square => square.Side * square.Side,
            _ => throw new NotSupportedException($"Unknown shape '{Request.Shape.GetType().Name}'."),
        };

        return Task.FromResult(new ShapeDescription
        {
            Shape = Request.Shape,
            Area = area,
            Metadata = new ShapeEnvelope
            {
                Label = "nested",
                FeaturedShape = Request.Shape,
            },
        });
    }
}
public sealed class UploadGreetingCardHandler : IRequestHandler<UploadGreetingCardRequest, UploadResponse>
{
    private readonly DocumentStore _documents;

    /// <summary>Initializes a new instance of the <see cref="UploadGreetingCardHandler"/> class.</summary>
    public UploadGreetingCardHandler(DocumentStore documents)
    {
        _documents = documents;
    }

    /// <summary>Stores a batch of uploaded attachments.</summary>
    public sealed class UploadGreetingCardsHandler : IRequestHandler<UploadGreetingCardsRequest, UploadBatchResponse>
    {
        private readonly DocumentStore _documents;

        /// <summary>Initializes a new instance.</summary>
        public UploadGreetingCardsHandler(DocumentStore documents)
        {
            _documents = documents;
        }

        /// <inheritdoc />
        public async Task<UploadBatchResponse> ExecuteAsync(UploadGreetingCardsRequest request, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var names = new List<string>();
            foreach (var attachment in request.Attachments)
            {
                await using var stream = attachment.OpenRead();
                await _documents.SaveAsync(Guid.NewGuid(), attachment.Name, attachment.ContentType, stream).ConfigureAwait(false);
                names.Add(attachment.Name);
            }

            return new UploadBatchResponse { Id = request.Id, Names = names };
        }
    }

    /// <inheritdoc />
    public async Task<UploadResponse> ExecuteAsync(UploadGreetingCardRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);

        await using var stream = Request.Attachment.OpenRead();
        var length = await _documents.SaveAsync(Request.Id, Request.Attachment.Name, Request.Attachment.ContentType, stream).ConfigureAwait(false);

        return new UploadResponse
        {
            Id = Request.Id,
            Name = Request.Attachment.Name,
            ContentType = Request.Attachment.ContentType,
            Length = length,
        };
    }
}

/// <summary>Loads previously uploaded attachments.</summary>
public sealed class GetDocumentHandler : IQueryHandler<GetDocumentQuery, IArkAttachment>
{
    private readonly DocumentStore _documents;

    /// <summary>Initializes a new instance of the <see cref="GetDocumentHandler"/> class.</summary>
    public GetDocumentHandler(DocumentStore documents)
    {
        _documents = documents;
    }

    /// <inheritdoc />
    public Task<IArkAttachment> ExecuteAsync(GetDocumentQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Task.FromResult(_documents.Get(query.Id)
            ?? throw new EntityNotFoundException($"Greeting card '{query.Id}' was not found."));
    }
}
