// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;

using Microsoft.AspNetCore.Routing;

using Rebus.Config;
using Rebus.Routing;

using SimpleInjector;

namespace Ark.Tools.MediatorFramework.Hosting.Contracts;

/// <summary>
/// Marker type selecting the synthetic contract assembly for generated transport mappings.
/// </summary>
public sealed class HostingMarker
{
}

/// <summary>
/// Invokes the source-generated Minimal API, gRPC, and Rebus mappings for the synthetic contracts.
/// </summary>
public static class HostingEndpointMappings
{
    /// <summary>Maps the synthetic HTTP endpoints.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public static void MapMinimalApi(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArkGeneratedEndpoints.MapArkEndpointsFromAssembly<HostingMarker>(
            endpoints,
            versionPrefix: "/api/v{version}");
    }

    /// <summary>Maps the synthetic code-first gRPC services.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public static void MapGrpc(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArkGeneratedEndpoints.MapArkGrpcServicesFromAssembly<HostingMarker>(endpoints);
    }

    /// <summary>Registers the generated Rebus handlers for the synthetic contracts.</summary>
    /// <param name="container">The SimpleInjector container.</param>
    public static void RegisterRebusHandlers(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArkGeneratedEndpoints.RegisterArkRebusHandlersFromAssembly<HostingMarker>(container);
    }

    /// <summary>Configures generated owner-queue routing for the synthetic Rebus messages.</summary>
    /// <param name="routing">The Rebus routing configuration.</param>
    public static void ConfigureRebusRouting(StandardConfigurer<IRouter> routing)
    {
        ArgumentNullException.ThrowIfNull(routing);
        ArkGeneratedEndpoints.ConfigureArkRebusRouting<HostingMarker>(routing);
    }
}

/// <summary>
/// Deterministic request contract with route, query, body, and server-owned properties.
/// </summary>
[HttpEndpoint("POST", "/hosting/requests/{id}", AllowAnonymous = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingRequest : Ark.Tools.Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the route identifier.</summary>
    [HttpRoute]
    [ProtoBuf.ProtoMember(1)]
    public int Id { get; set; }

    /// <summary>Gets or sets the optional query filter.</summary>
    [HttpQuery]
    [ProtoBuf.ProtoMember(2)]
    public string? Filter { get; set; }

    /// <summary>Gets or sets the request body value.</summary>
    [ProtoBuf.ProtoMember(3)]
    public string Value { get; set; } = string.Empty;

    /// <summary>Gets or sets the server-owned stamp.</summary>
    [ServerSet]
    [ProtoBuf.ProtoMember(4)]
    public string? ServerStamp { get; set; }
}

/// <summary>Response returned by deterministic hosting handlers.</summary>
[ProtoBuf.ProtoContract]
public sealed record HostingResponse
{
    /// <summary>Gets or sets the response message.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the server-owned response stamp.</summary>
    [ProtoBuf.ProtoMember(2)]
    public string ServerStamp { get; set; } = string.Empty;
}

/// <summary>Query contract with route and query parameters.</summary>
[HttpEndpoint("GET", "/hosting/queries/{id}", AllowAnonymous = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingQuery : Ark.Tools.Solid.IQuery<HostingResponse>
{
    /// <summary>Gets or sets the route identifier.</summary>
    [HttpRoute]
    [ProtoBuf.ProtoMember(1)]
    public int Id { get; set; }

    /// <summary>Gets or sets the optional query value.</summary>
    [HttpQuery]
    [ProtoBuf.ProtoMember(2)]
    public string? Value { get; set; }
}

/// <summary>Command contract exposed through HTTP, gRPC, and Rebus.</summary>
[HttpEndpoint("POST", "/hosting/commands", AllowAnonymous = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[RebusMessage(OwnerQueue = "hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingCommand : Ark.Tools.Solid.ICommand
{
    /// <summary>Gets or sets the command value.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Rebus-only command contract.</summary>
[RebusMessage(OwnerQueue = "hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingRebusCommand : Ark.Tools.Solid.ICommand
{
    /// <summary>Gets or sets the command value.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Request whose handler produces a validation failure.</summary>
[HttpEndpoint("POST", "/hosting/validation", AllowAnonymous = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingValidationRequest : Ark.Tools.Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the value to validate.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Request whose handler produces a business-rule violation.</summary>
[HttpEndpoint("POST", "/hosting/business-violation", AllowAnonymous = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingBusinessViolationRequest : Ark.Tools.Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the value that violates the business rule.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Query returning a deterministic asynchronous stream.</summary>
[HttpEndpoint("GET", "/hosting/stream", AllowAnonymous = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingStreamQuery : Ark.Tools.Solid.IQuery<IAsyncEnumerable<HostingStreamItem>>
{
    /// <summary>Gets or sets the number of items to produce.</summary>
    [HttpQuery]
    [ProtoBuf.ProtoMember(1)]
    public int Count { get; set; }
}

/// <summary>Item returned by the synthetic streaming query.</summary>
[ProtoBuf.ProtoContract]
public sealed record HostingStreamItem
{
    /// <summary>Gets or sets the item number.</summary>
    [ProtoBuf.ProtoMember(1)]
    public int Number { get; set; }
}

/// <summary>Multipart request containing one transport-agnostic attachment.</summary>
[HttpEndpoint("POST", "/hosting/attachments", AllowAnonymous = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingAttachmentUploadRequest : Ark.Tools.Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the uploaded attachment.</summary>
    [ProtoBuf.ProtoMember(1)]
    public Ark.MediatorFramework.IArkAttachment? Attachment { get; set; }
}

/// <summary>Versioned query used to exercise contract lifetime metadata.</summary>
[HttpEndpoint("GET", "/hosting/versioned", AllowAnonymous = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[Versioning(Introduced = 2, Retired = 4)]
[ProtoBuf.ProtoContract]
public sealed record HostingVersionedQuery : Ark.Tools.Solid.IQuery<HostingResponse>
{
    /// <summary>Gets or sets the request value.</summary>
    [HttpQuery]
    [ProtoBuf.ProtoMember(1)]
    public string? Value { get; set; }
}
