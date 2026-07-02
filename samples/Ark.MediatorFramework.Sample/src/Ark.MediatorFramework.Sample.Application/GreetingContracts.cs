// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Response returned by the greeting operations.</summary>
public sealed record GreetingResponse
{
    /// <summary>Gets the greeting identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the greeting message.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// Pure transport-agnostic request (mutation). Transport is declared explicitly and per-transport:
/// <see cref="HttpEndpointAttribute"/> exposes it as an HTTP POST and <see cref="RebusMessageAttribute"/>
/// exposes it as a Rebus message; the generator emits the wiring for each declared transport.
/// </summary>
[HttpEndpoint("POST", "/api/v1/greetings")]
[RebusMessage]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>
{
    /// <summary>Gets the name to greet.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Pure transport-agnostic query (read). Declared with <see cref="HttpEndpointAttribute"/> only,
/// so the generator exposes it as an HTTP GET (a query is a read, not a bus message).
/// </summary>
[HttpEndpoint("GET", "/api/v1/greetings/{id}")]
public sealed record GetGreetingQuery(Guid Id) : IQuery<GreetingResponse>;
