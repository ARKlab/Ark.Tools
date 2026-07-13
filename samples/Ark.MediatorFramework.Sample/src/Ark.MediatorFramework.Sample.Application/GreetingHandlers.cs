// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core.BusinessRuleViolation;

using FluentValidation;
using FluentValidation.Results;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application;

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

        // Semantic domain validation: the handler throws a transport-agnostic ValidationException;
        // each transport maps it to its own error shape (Minimal API -> ProblemDetails 400).
        if (string.IsNullOrWhiteSpace(Request.Name))
            throw new ValidationException([new ValidationFailure(nameof(Request.Name), "Name must not be empty.")]);

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
    /// <inheritdoc />
    public async Task<UploadResponse> ExecuteAsync(UploadGreetingCardRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);

        await using var stream = Request.Attachment.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ctk).ConfigureAwait(false);

        return new UploadResponse
        {
            Name = Request.Attachment.Name,
            ContentType = Request.Attachment.ContentType,
            Length = buffer.Length,
        };
    }
}
