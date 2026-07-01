// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Api;

/// <summary>Pure handler for <see cref="CreateGreetingRequest"/> — no transport types.</summary>
public sealed class CreateGreetingHandler : IRequestHandler<CreateGreetingRequest, GreetingResponse>
{
    private readonly IGreetingStore _store;
    private readonly IUserContext _user;

    /// <summary>Initializes a new instance of the <see cref="CreateGreetingHandler"/> class.</summary>
    public CreateGreetingHandler(IGreetingStore store, IUserContext user)
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
            Message = $"Hello, {Request.Name}! (by {_user.UserId ?? "anonymous"})",
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
