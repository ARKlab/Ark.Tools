// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using FluentValidation;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions;

/// <summary>Response returned by the echo contracts.</summary>
public sealed record EchoResponse
{
    /// <summary>Gets the echoed identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the echoed message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the echoed count.</summary>
    public int Count { get; init; }
}

/// <summary>Query exercising route and query binding with validation.</summary>
[HttpEndpoint("GET", "/api/v{version}/echo/{id}")]
public sealed record EchoQuery : IQuery<EchoQuery, EchoResponse>
{
    /// <summary>Gets the route identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the optional message.</summary>
    [HttpQuery]
    public string? Message { get; init; }

    /// <summary>Gets the count, validated to be within 1-100.</summary>
    [HttpQuery]
    public int Count { get; init; } = 1;
}

/// <summary>Request exercising JSON body binding on a record contract.</summary>
[HttpEndpoint("POST", "/api/v{version}/echo")]
public sealed record EchoRequest : IRequest<EchoRequest, EchoResponse>
{
    /// <summary>Gets the message to echo, must not be empty.</summary>
    public required string Message { get; init; }
}

/// <summary>Anonymous probe endpoint.</summary>
[HttpEndpoint("GET", "/api/v{version}/ping", AllowAnonymous = true)]
public sealed record PingQuery : IQuery<PingQuery, EchoResponse>
{
}

/// <summary>Validates <see cref="EchoQuery"/>.</summary>
public sealed class EchoQueryValidator : AbstractValidator<EchoQuery>
{
    /// <summary>Initializes a new instance of the <see cref="EchoQueryValidator"/> class.</summary>
    public EchoQueryValidator()
    {
        RuleFor(static query => query.Count).InclusiveBetween(1, 100);
    }
}

/// <summary>Validates <see cref="EchoRequest"/>.</summary>
public sealed class EchoRequestValidator : AbstractValidator<EchoRequest>
{
    /// <summary>Initializes a new instance of the <see cref="EchoRequestValidator"/> class.</summary>
    public EchoRequestValidator()
    {
        RuleFor(static request => request.Message).NotEmpty();
    }
}

/// <summary>Handles <see cref="EchoQuery"/>.</summary>
public sealed class EchoQueryHandler : IQueryHandler<EchoQuery, EchoResponse>
{
    /// <inheritdoc />
    public async Task<EchoResponse> ExecuteAsync(EchoQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await Task.FromResult(new EchoResponse { Id = query.Id, Message = query.Message ?? string.Empty, Count = query.Count }).ConfigureAwait(false);
    }
}

/// <summary>Handles <see cref="EchoRequest"/>.</summary>
public sealed class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
{
    /// <inheritdoc />
    public async Task<EchoResponse> ExecuteAsync(EchoRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Task.FromResult(new EchoResponse { Message = request.Message }).ConfigureAwait(false);
    }
}

/// <summary>Handles <see cref="PingQuery"/>.</summary>
public sealed class PingQueryHandler : IQueryHandler<PingQuery, EchoResponse>
{
    /// <inheritdoc />
    public async Task<EchoResponse> ExecuteAsync(PingQuery query, CancellationToken ctk = default)
    {
        return await Task.FromResult(new EchoResponse { Message = "pong" }).ConfigureAwait(false);
    }
}
