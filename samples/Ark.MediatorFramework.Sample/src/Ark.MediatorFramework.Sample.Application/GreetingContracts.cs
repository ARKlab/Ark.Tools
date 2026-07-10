// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using ProtoBuf;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Response returned by the greeting operations.</summary>
[ProtoContract]
public sealed record GreetingResponse
{
    /// <summary>Gets the greeting identifier.</summary>
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    /// <summary>Gets the greeting message.</summary>
    [ProtoMember(2)]
    public required string Message { get; init; }
}

/// <summary>
/// Pure transport-agnostic request (mutation). Transport is declared explicitly and per-transport:
/// <see cref="HttpEndpointAttribute"/> exposes it as an HTTP POST and <see cref="RebusMessageAttribute"/>
/// exposes it as a Rebus message; the generator emits the wiring for each declared transport.
/// </summary>
[HttpEndpoint("POST", "/api/v1/greetings")]
[RebusMessage]
[GrpcMethod]
[ServiceGroup("Greetings")]
[ProtoContract]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>
{
    /// <summary>Gets the name to greet.</summary>
    [ProtoMember(1)]
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Pure transport-agnostic query (read). Declared with <see cref="HttpEndpointAttribute"/> only,
/// so the generator exposes it as an HTTP GET (a query is a read, not a bus message).
/// </summary>
[HttpEndpoint("GET", "/api/v1/greetings/{id}")]
public sealed record GetGreetingQuery(Guid Id) : IQuery<GreetingResponse>;

/// <summary>Version 2 of the greeting response, evolving the contract with the message length.</summary>
public sealed record GreetingResponseV2
{
    /// <summary>Gets the greeting identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the greeting message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the message length (added in v2).</summary>
    public required int MessageLength { get; init; }
}

/// <summary>
/// Version 2 read exposed under the <c>/api/v2</c> route. The generator infers the API version group
/// from the route template, so this endpoint lands in the <c>v2</c> OpenAPI document while the v1
/// endpoints stay in <c>v1</c>, demonstrating route-based API versioning.
/// </summary>
[HttpEndpoint("GET", "/api/v2/greetings/{id}")]
public sealed record GetGreetingV2Query(Guid Id) : IQuery<GreetingResponseV2>;
