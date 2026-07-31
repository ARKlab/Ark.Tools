// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

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

    /// <summary>Gets the audit identifier associated with this entity version.</summary>
    [ProtoMember(7)]
    public Guid AuditId { get; init; }

    /// <summary>Gets the opaque concurrency token echoed in update preconditions.</summary>
    [ProtoMember(8)]
    [ETag]
    public string? ETag { get; init; }
}

/// <summary>Updates a greeting using an opaque ETag precondition.</summary>
[HttpEndpoint("PUT", "/api/v{version}/greetings/{id}")]
[GrpcMethod("UpdateGreetingMessage")]
[GrpcService("Greetings")]
[RequireScopePolicy(ApplicationScopes.GreetingWrite)]
[ProtoContract]
public sealed record UpdateGreetingMessageRequest : IRequest<GreetingResponse>
{
    /// <summary>Gets the greeting identifier.</summary>
    [ProtoMember(1)]
    public Guid Id { get; init; }

    /// <summary>Gets the replacement message.</summary>
    [ProtoMember(2)]
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the opaque concurrency token.</summary>
    [ProtoMember(3)]
    [ETag]
    public string? ETag { get; init; }
}

/// <summary>Command used to exercise the synchronous command transport contract.</summary>
[HttpEndpoint("POST", "/api/v{version}/greetings/refresh")]
[GrpcMethod("RefreshGreeting")]
[GrpcService("Greetings")]
[ProtoContract]
public sealed record RefreshGreetingCommand : ICommand
{
    /// <summary>Gets the greeting identifier to refresh.</summary>
    [ProtoMember(1)]
    public Guid Id { get; init; }
}

/// <summary>
/// Pure transport-agnostic request (mutation). Transport is declared explicitly and per-transport:
/// <see cref="HttpEndpointAttribute"/> exposes it as an HTTP POST and <see cref="RebusMessageAttribute"/>
/// exposes it as a Rebus message; the generator emits the wiring for each declared transport.
/// </summary>
[HttpEndpoint("POST", "/api/v{version}/greetings", AcceptsMessagePack = true, SuccessStatusCode = 201)]
[RebusMessage]
[GrpcMethod("CreateGreeting")]
[GrpcService("Greetings")]
[RequireScopePolicy(ApplicationScopes.GreetingWrite)]
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
[ApiGroup("Greetings")]
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

/// <summary>Notification emitted by the SQL transaction after a greeting is persisted.</summary>
[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record GreetingCreatedNotification : ICommand
{
    /// <summary>Gets the persisted greeting.</summary>
    public required GreetingResponse Greeting { get; init; }
}

/// <summary>
/// Pure transport-agnostic query (read). Declared with <see cref="HttpEndpointAttribute"/> only,
/// so the generator exposes it as an HTTP GET (a query is a read, not a bus message).
/// </summary>
[Versioning(Introduced = 1, Retired = 2)]
[HttpEndpoint("GET", "/greetings/{id}")]
[GrpcMethod("GetGreeting")]
[GrpcService("Greetings")]
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
[Versioning(Introduced = 2)]
[HttpEndpoint("GET", "/api/v{version}/greetings-v2/{id}")]
[GrpcMethod("GetGreeting")]
[GrpcService("Greetings")]
[ProtoContract]
public sealed record GetGreetingV2Query : IQuery<GreetingResponseV2>
{
    /// <summary>Gets the greeting identifier.</summary>
    [ProtoMember(1)]
    public Guid Id { get; init; }
}

/// <summary>Searches greetings using a validated offset and limit.</summary>
[HttpEndpoint("GET", "/api/v{version}/greetings")]
[GrpcMethod("SearchGreetings")]
[GrpcService("Greetings")]
[ProtoContract]
public sealed record SearchGreetingsQuery : IQuery<GreetingPage>, IQueryPaged
{
    /// <summary>Gets the optional message filter.</summary>
    [BindFromQuery]
    [ProtoMember(1)]
    public string? MessageContains { get; init; }

    /// <inheritdoc />
    [BindFromQuery]
    [ProtoMember(2)]
    public int Skip { get; set; }

    /// <inheritdoc />
    [BindFromQuery]
    [ProtoMember(3)]
    public int Limit { get; init; } = 25;

    /// <inheritdoc />
    [BindFromQuery]
    [ProtoMember(4)]
    public IEnumerable<string> Sort { get; init; } = [];
}

/// <summary>Item yielded by the incremental greeting stream.</summary>
[ProtoContract]
public sealed record GreetingStreamItem
{
    /// <summary>Gets the zero-based item index.</summary>
    [ProtoMember(1)]
    public int Index { get; init; }

    /// <summary>Gets the greeting message.</summary>
    [ProtoMember(2)]
    public string Message { get; init; } = string.Empty;
}

/// <summary>Streams greetings without buffering the complete result.</summary>
[HttpEndpoint("GET", "/api/v{version}/greetings/stream")]
[GrpcMethod("GetGreetingsStream")]
[GrpcService("Greetings")]
[RequireScopePolicy(ApplicationScopes.GreetingWrite)]
[ProtoContract]
public sealed record GetGreetingsStreamQuery : IQuery<IAsyncEnumerable<GreetingStreamItem>>
{
    /// <summary>Gets the number of items to yield.</summary>
    [BindFromQuery]
    [ProtoMember(1)]
    public int Count { get; init; }

    /// <summary>Gets the delay between yielded items in milliseconds.</summary>
    [BindFromQuery]
    [ProtoMember(2)]
    public int DelayMilliseconds { get; init; }
}

/// <summary>Page of greetings returned by <see cref="SearchGreetingsQuery"/>.</summary>
[ProtoContract]
public sealed record GreetingPage
{
    /// <summary>Gets the total number of matching greetings.</summary>
    [ProtoMember(1)]
    public long Count { get; init; }

    /// <summary>Gets the requested offset.</summary>
    [ProtoMember(2)]
    public int Skip { get; init; }

    /// <summary>Gets the requested page size.</summary>
    [ProtoMember(3)]
    public int Limit { get; init; }

    /// <summary>Gets the greetings on this page.</summary>
    [ProtoMember(4)]
    public GreetingResponse[] Data { get; init; } = [];
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
