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
/// Pure transport-agnostic request (mutation). Decorated with <see cref="ArkEndpointAttribute"/>
/// so the hosting assembly can expose it as an HTTP POST and a Rebus message via the generator.
/// </summary>
[ArkEndpoint("POST", "/api/v1/greetings")]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>
{
    /// <summary>Gets the name to greet.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Pure transport-agnostic query (read). Decorated with <see cref="ArkEndpointAttribute"/>
/// so the hosting assembly can expose it as an HTTP GET via the generator.
/// </summary>
[ArkEndpoint("GET", "/api/v1/greetings/{id}")]
public sealed record GetGreetingQuery(Guid Id) : IQuery<GreetingResponse>;
