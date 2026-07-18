// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core.BusinessRuleViolation;

using FluentValidation;
using FluentValidation.Results;

using Rebus.Bus;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application;

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

/// <summary>Pure handler for <see cref="CreateGreetingRequest"/> — no transport types.</summary>
public sealed class CreateGreetingHandler : IRequestHandler<CreateGreetingRequest, GreetingResponse>
{
    private readonly IGreetingStore _store;
    private readonly IContextProvider<ClaimsPrincipal> _user;

    /// <summary>Initializes a new instance of the <see cref="CreateGreetingHandler"/> class.</summary>
    public CreateGreetingHandler(IGreetingStore store, IContextProvider<ClaimsPrincipal> user)
    {
        _store = store;
        _user = user;
    }

    /// <inheritdoc />
    public Task<GreetingResponse> ExecuteAsync(CreateGreetingRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);

        if (_store.All().Any(g => g.Message.Contains($"Hello, {Request.Name}!", StringComparison.Ordinal)))
            throw new BusinessRuleViolationException(new GreetingAlreadyExistsViolation(Request.Name));

        var response = new GreetingResponse
        {
            Id = Guid.NewGuid(),
            Message = $"Hello, {Request.Name}! (by {_user.GetUserId() ?? "anonymous"})",
            Date = Request.Date,
            DateTime = Request.DateTime,
            OffsetDateTime = Request.OffsetDateTime,
            Period = Request.Period,
        };

        _store.Save(response);
        return Task.FromResult(response);
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
        await _bus.SendLocal(new CompleteGreetingCompositionRequest
        {
            Id = id,
            Name = Request.Name,
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
    private readonly IGreetingStore _store;

    /// <summary>Initializes a new instance of the <see cref="CompleteGreetingCompositionHandler"/> class.</summary>
    public CompleteGreetingCompositionHandler(IGreetingStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<GreetingResponse> ExecuteAsync(CompleteGreetingCompositionRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);

        var response = new GreetingResponse
        {
            Id = Request.Id,
            Message = $"Hello, {Request.Name}! (async)",
        };

        _store.Save(response);
        return Task.FromResult(response);
    }
}

/// <summary>Pure handler for <see cref="GetGreetingQuery"/> — no transport types.</summary>
public sealed class GetGreetingHandler : IQueryHandler<GetGreetingQuery, GreetingResponse>
{
    private readonly IGreetingStore _store;

    /// <summary>Initializes a new instance of the <see cref="GetGreetingHandler"/> class.</summary>
    public GetGreetingHandler(IGreetingStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<GreetingResponse> ExecuteAsync(GetGreetingQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Task.FromResult(_store.Get(query.Id));
    }
}

/// <summary>Pure handler for <see cref="GetGreetingV2Query"/> — no transport types.</summary>
public sealed class GetGreetingV2Handler : IQueryHandler<GetGreetingV2Query, GreetingResponseV2>
{
    private readonly IGreetingStore _store;

    /// <summary>Initializes a new instance of the <see cref="GetGreetingV2Handler"/> class.</summary>
    public GetGreetingV2Handler(IGreetingStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<GreetingResponseV2> ExecuteAsync(GetGreetingV2Query query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var greeting = _store.Get(query.Id);
        return Task.FromResult(new GreetingResponseV2
        {
            Id = greeting.Id,
            Message = greeting.Message,
            MessageLength = greeting.Message.Length,
        });
    }
}

/// <summary>Pure handler for <see cref="UpdateGreetingRequest"/>.</summary>
public sealed class UpdateGreetingHandler : IRequestHandler<UpdateGreetingRequest, EnvelopeBindingResponse>
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
            Message = Request.Message,
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

    /// <inheritdoc />
    public async Task<UploadResponse> ExecuteAsync(UploadGreetingCardRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);

        await using var stream = Request.Attachment.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ctk).ConfigureAwait(false);
        _documents.Save(Request.Id, Request.Attachment.Name, Request.Attachment.ContentType, buffer.ToArray());

        return new UploadResponse
        {
            Id = Request.Id,
            Name = Request.Attachment.Name,
            ContentType = Request.Attachment.ContentType,
            Length = buffer.Length,
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
        return Task.FromResult(_documents.Get(query.Id)!);
    }
}
