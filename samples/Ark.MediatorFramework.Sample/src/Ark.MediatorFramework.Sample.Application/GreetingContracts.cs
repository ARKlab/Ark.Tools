// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Authorization;

using NodaTime;

using MessagePack;

using ProtoBuf;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Response returned by the greeting operations.</summary>
[ProtoContract]
[MessagePackObject(true)]
public sealed record GreetingResponse
{
    /// <summary>Gets the greeting identifier.</summary>
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    /// <summary>Gets the greeting message.</summary>
    [ProtoMember(2)]
    public required string Message { get; init; }

    /// <summary>Gets the representative local date.</summary>
    [ProtoMember(3)]
    public LocalDate Date { get; init; }

    /// <summary>Gets the representative local date and time.</summary>
    [ProtoMember(4)]
    public LocalDateTime DateTime { get; init; }

    /// <summary>Gets the representative offset date and time.</summary>
    [ProtoMember(5)]
    public OffsetDateTime OffsetDateTime { get; init; }

    /// <summary>Gets the representative period.</summary>
    [ProtoMember(6)]
    public Period Period { get; init; } = Period.Zero;
}

/// <summary>
/// Pure transport-agnostic request (mutation). Transport is declared explicitly and per-transport:
/// <see cref="HttpEndpointAttribute"/> exposes it as an HTTP POST and <see cref="RebusMessageAttribute"/>
/// exposes it as a Rebus message; the generator emits the wiring for each declared transport.
/// </summary>
[HttpEndpoint("POST", "/api/v{version}/greetings", AcceptsMessagePack = true)]
[RebusMessage]
[GrpcMethod("CreateGreeting")]
[ServiceGroup("Greetings")]
[PolicyAuthorize("greeting.write")]
[ProtoContract]
[MessagePackObject(true)]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>
{
    /// <summary>Gets the name to greet.</summary>
    [ProtoMember(1)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the representative local date.</summary>
    [ProtoMember(2)]
    public LocalDate Date { get; init; }

    /// <summary>Gets the representative local date and time.</summary>
    [ProtoMember(3)]
    public LocalDateTime DateTime { get; init; }

    /// <summary>Gets the representative offset date and time.</summary>
    [ProtoMember(4)]
    public OffsetDateTime OffsetDateTime { get; init; }

    /// <summary>Gets the representative period.</summary>
    [ProtoMember(5)]
    public Period Period { get; init; } = Period.Zero;

    /// <summary>Gets the authenticated user populated by the server.</summary>
    [ServerSet]
    public string? UserId { get; init; }
}

/// <summary>HTTP-only request that publishes work to Rebus and returns immediately.</summary>
[HttpEndpoint("POST", "/api/v{version}/greetings/compose")]
public sealed record ComposeGreetingRequest : IRequest<ComposeGreetingResponse>
{
    /// <summary>Gets the name to greet.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>Response returned when a composition request has been queued.</summary>
public sealed record ComposeGreetingResponse
{
    /// <summary>Gets the greeting identifier assigned to the queued workflow.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the workflow state.</summary>
    public string Status { get; init; } = string.Empty;
}

/// <summary>Rebus-only request completed asynchronously by the composition workflow.</summary>
[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record CompleteGreetingCompositionRequest : IRequest<GreetingResponse>
{
    /// <summary>Gets the greeting identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the name to greet.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Pure transport-agnostic query (read). Declared with <see cref="HttpEndpointAttribute"/> only,
/// so the generator exposes it as an HTTP GET (a query is a read, not a bus message).
/// </summary>
[HttpEndpoint("GET", "/api/v{version}/greetings/{id}", RetiredIn = 2)]
[GrpcMethod("GetGreeting", RetiredIn = 2)]
[ServiceGroup("Greetings")]
[ProtoContract]
public sealed record GetGreetingQuery : IQuery<GreetingResponse>
{
    /// <summary>Gets the greeting identifier.</summary>
    [ProtoMember(1)]
    public Guid Id { get; init; }
}

/// <summary>Version 2 of the greeting response, evolving the contract with the message length.</summary>
[ProtoContract]
public sealed record GreetingResponseV2
{
    /// <summary>Gets the greeting identifier.</summary>
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    /// <summary>Gets the greeting message.</summary>
    [ProtoMember(2)]
    public required string Message { get; init; }

    /// <summary>Gets the message length (added in v2).</summary>
    [ProtoMember(3)]
    public required int MessageLength { get; init; }
}

/// <summary>
/// Version 2 read exposed under the versioned replacement route. The generator expands the route
/// once for each active API version and places it in the corresponding OpenAPI document.
/// </summary>
[HttpEndpoint("GET", "/api/v{version}/greetings-v2/{id}", IntroducedIn = 2)]
[GrpcMethod("GetGreeting", IntroducedIn = 2)]
[ServiceGroup("Greetings")]
[ProtoContract]
public sealed record GetGreetingV2Query : IQuery<GreetingResponseV2>
{
    /// <summary>Gets the greeting identifier.</summary>
    [ProtoMember(1)]
    public Guid Id { get; init; }
}

/// <summary>Response proving route, query and body values were combined.</summary>
public sealed record EnvelopeBindingResponse
{
    /// <summary>Gets the route identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the query value.</summary>
    public required string Audit { get; init; }

    /// <summary>Gets the body value.</summary>
    public required string Message { get; init; }
}

/// <summary>Request demonstrating combined Minimal API envelope binding.</summary>
[HttpEndpoint("POST", "/api/v{version}/greetings/{id}/envelope")]
public sealed record UpdateGreetingRequest : IRequest<EnvelopeBindingResponse>
{
    /// <summary>Gets the route identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the query value.</summary>
    [BindFromQuery]
    public string Audit { get; init; } = string.Empty;

    /// <summary>Gets the body value.</summary>
    public string Message { get; init; } = string.Empty;
}
