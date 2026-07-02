// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

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

        var response = new GreetingResponse
        {
            Id = Guid.NewGuid(),
            Message = $"Hello, {Request.Name}! (by {_user.GetUserId() ?? "anonymous"})",
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

/// <summary>Pure handler for <see cref="UploadGreetingCardRequest"/> reading the attachment stream.</summary>
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
