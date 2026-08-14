// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;

using Ark.MediatorFramework;
using Ark.MediatorFramework.Generated;
using Ark.Tools.Authorization;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.MediatorFramework.Rebus;

using MessagePack;
using NodaTime;

using Microsoft.AspNetCore.Routing;

using Rebus.Config;
using SimpleInjector;

using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Hosting.Contracts;

/// <summary>
/// Marker type selecting the synthetic contract assembly for generated transport mappings.
/// </summary>
public sealed class HostingMarker
{
}

/// <summary>
/// Explicit source-generation context for the synthetic hosting contracts.
/// </summary>
[ArkGenerateMinimalApiForAssembly(typeof(HostingMarker))]
public partial class HostingMinimalApiContext
{
}

/// <summary>
/// Explicit gRPC source-generation context for the synthetic hosting contracts.
/// </summary>
[ArkGenerateGrpcForAssembly(typeof(HostingMarker))]
public partial class HostingGrpcContext
{
}

/// <summary>
/// Explicit Rebus source-generation context for the synthetic hosting contracts.
/// </summary>
[ArkGenerateRebusForAssembly(typeof(HostingMarker))]
public partial class HostingRebusContext
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
        ArkGeneratedEndpoints.MapArkEndpoints<HostingMinimalApiContext>(
            endpoints,
            versionPrefix: "/api/v{version}");
    }

    /// <summary>Maps the synthetic code-first gRPC services.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public static void MapGrpc(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArkGeneratedEndpoints.MapArkGrpcServices<HostingGrpcContext>(endpoints);
    }

    /// <summary>Registers the generated Rebus handlers for the synthetic contracts.</summary>
    /// <param name="container">The SimpleInjector container.</param>
    public static void RegisterRebusHandlers(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArkGeneratedEndpoints.RegisterArkRebusHandlers<HostingRebusContext>(container);
    }

    /// <summary>Configures generated owner-queue routing for the synthetic Rebus messages.</summary>
    /// <param name="routing">The Rebus routing configuration.</param>
    public static void ConfigureRebusRouting(StandardConfigurer<global::Rebus.Routing.IRouter> routing)
    {
        ArgumentNullException.ThrowIfNull(routing);
        ArkGeneratedEndpoints.ConfigureArkRebusRouting<HostingRebusContext>(routing);
    }
}

/// <summary>
/// Deterministic request contract with route, query, body, and server-owned properties.
/// </summary>
[HttpEndpoint("POST", "/api/v{version}/hosting/requests/{id}", AllowAnonymous = true, AcceptsMessagePack = true)]
[GrpcMethod]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract(Name = "HostingRequestMessage")]
[MessagePackObject(true)]
public sealed record HostingRequest : Solid.IRequest<HostingResponse>
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
[MessagePackObject(true)]
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
[HttpEndpoint("GET", "/api/v{version}/hosting/queries/{id}", AllowAnonymous = true)]
[GrpcMethod("ExecuteHostingQuery")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingQuery : Solid.IQuery<HostingResponse>
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
[HttpEndpoint("POST", "/api/v{version}/hosting/commands", AllowAnonymous = true)]
[GrpcMethod("ExecuteHostingCommand")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingCommand : Solid.ICommand
{
    /// <summary>Gets or sets the command value.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Rebus-only command contract.</summary>
[RebusMessage(OwnerQueue = "hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingRebusCommand : Solid.ICommand
{
    /// <summary>Gets or sets the command value.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Command whose handler fails so the hosting tests can verify retry exhaustion.</summary>
[RebusMessage(OwnerQueue = "hosting")]
public sealed record HostingRetryCommand : Solid.ICommand;

/// <summary>Command whose handler fails so the hosting tests can verify second-level retry handling.</summary>
[RebusMessage(OwnerQueue = "hosting")]
public sealed record HostingSecondLevelRetryCommand : Solid.ICommand;

/// <summary>Command whose handler records the cancellation token supplied by Rebus.</summary>
[RebusMessage(OwnerQueue = "hosting")]
public sealed record HostingCancellationCommand : Solid.ICommand;

/// <summary>Command whose handler schedules a deferred delivery.</summary>
[RebusMessage(OwnerQueue = "hosting")]
public sealed record HostingDeferredCommand : Solid.ICommand;

/// <summary>Request whose handler produces a validation failure.</summary>
[HttpEndpoint("POST", "/api/v{version}/hosting/validation", AllowAnonymous = true)]
[GrpcMethod("ValidateHostingRequest")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingValidationRequest : Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the value to validate.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Request whose handler returns a configured success status.</summary>
[HttpEndpoint("POST", "/api/v{version}/hosting/status", AllowAnonymous = true, SuccessStatusCode = 201)]
public sealed record HostingStatusRequest : Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the request value.</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>Query whose handler returns no value.</summary>
[HttpEndpoint("GET", "/api/v{version}/hosting/not-found", AllowAnonymous = true, NullResultStatusCode = 404)]
[GrpcMethod("GetHostingNotFound")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingNotFoundQuery : Solid.IQuery<HostingResponse>;

/// <summary>Request whose handler produces a business-rule violation.</summary>
[HttpEndpoint("POST", "/api/v{version}/hosting/business-violation", AllowAnonymous = true)]
[GrpcMethod("TriggerHostingBusinessViolation")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingBusinessViolationRequest : Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the value that violates the business rule.</summary>
    [ProtoBuf.ProtoMember(1)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Request whose handler produces an unexpected exception.</summary>
[HttpEndpoint("POST", "/api/v{version}/hosting/unexpected", AllowAnonymous = true)]
public sealed record HostingUnexpectedRequest : Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the request value.</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>Query protected by the transport-agnostic authorization decorator.</summary>
[HttpEndpoint("GET", "/api/v{version}/hosting/authorized", AllowAnonymous = false)]
[GrpcMethod("GetHostingAuthorized")]
[GrpcService("Hosting")]
[PolicyAuthorize(typeof(HostingScopePolicy))]
[ProtoBuf.ProtoContract]
public sealed record HostingAuthorizedQuery : Solid.IQuery<HostingResponse>;

/// <summary>Query returning the authenticated synthetic caller.</summary>
[GrpcMethod("GetHostingUserContext")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingUserContextQuery : Solid.IQuery<HostingResponse>;

/// <summary>Request whose handler reports an opaque ETag mismatch.</summary>
[GrpcMethod("CheckHostingETag")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingETagMismatchRequest : Solid.IRequest<HostingResponse>
{
    /// <summary>Gets the opaque ETag supplied by the caller.</summary>
    [ProtoBuf.ProtoMember(1)]
    [ETag]
    public string ETag { get; set; } = string.Empty;
}

/// <summary>Request whose handler reports an optimistic concurrency conflict.</summary>
[GrpcMethod("CheckHostingConcurrency")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingOptimisticConcurrencyRequest : Solid.IRequest<HostingResponse>
{
    /// <summary>Gets the opaque concurrency token supplied by the caller.</summary>
    [ProtoBuf.ProtoMember(1)]
    [ETag]
    public string ETag { get; set; } = string.Empty;
}

/// <summary>Policy requiring the synthetic hosting scope.</summary>
public sealed class HostingScopePolicy : IAuthorizationPolicy
{
    /// <summary>Initializes a new instance of the <see cref="HostingScopePolicy"/> class.</summary>
    public HostingScopePolicy()
    {
        var policy = new AuthorizationPolicyBuilder(nameof(HostingScopePolicy))
            .RequireClaim("scope", "hosting.test")
            .Build();
        Name = policy.Name;
        Requirements = policy.Requirements;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<IAuthorizationRequirement> Requirements { get; }
}

/// <summary>Query returning a deterministic asynchronous stream.</summary>
[HttpEndpoint("GET", "/api/v{version}/hosting/stream", AllowAnonymous = true)]
[GrpcMethod("StreamHosting")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingStreamQuery : Solid.IQuery<IAsyncEnumerable<HostingStreamItem>>
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
[HttpEndpoint(
    "POST",
    "/api/v{version}/hosting/attachments",
    AllowAnonymous = true,
    MaxRequestBodySizeBytes = 1024,
    MaxFileCount = 1,
    AllowedContentTypes = ["text/plain"])]
[GrpcMethod("UploadHostingAttachment")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingAttachmentUploadRequest : Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the uploaded attachment.</summary>
    [ProtoBuf.ProtoMember(1)]
    public IArkAttachment? Attachment { get; set; }
}

/// <summary>Multipart request containing multiple transport-agnostic attachments.</summary>
[HttpEndpoint(
    "POST",
    "/api/v{version}/hosting/attachments/multiple",
    AllowAnonymous = true,
    MaxRequestBodySizeBytes = 1024,
    MaxFileCount = 2,
    AllowedContentTypes = ["text/plain"])]
public sealed record HostingAttachmentCollectionUploadRequest : Solid.IRequest<HostingResponse>
{
    /// <summary>Gets or sets the uploaded attachments.</summary>
    public IReadOnlyList<IArkAttachment> Attachments { get; set; } = [];
}

/// <summary>Query returning a downloadable synthetic attachment.</summary>
[HttpEndpoint("GET", "/api/v{version}/hosting/attachments/{name}", AllowAnonymous = true)]
public sealed record HostingAttachmentDownloadQuery : Solid.IQuery<IArkAttachment>
{
    /// <summary>Gets or sets the attachment name.</summary>
    [HttpRoute]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Response model used to verify generated OpenAPI schemas.</summary>
public sealed record HostingOpenApiResponse
{
    /// <summary>Gets or sets the representative date.</summary>
    public LocalDate Date { get; init; }

    /// <summary>Gets or sets the polymorphic shape.</summary>
    public HostingShape Shape { get; init; } = new HostingCircle();

    /// <summary>Gets or sets the server-owned response stamp.</summary>
    [ServerSet]
    public string ServerStamp { get; init; } = string.Empty;
}

/// <summary>Query used to expose the generated OpenAPI response schema.</summary>
[HttpEndpoint("GET", "/api/v{version}/hosting/openapi", AllowAnonymous = true)]
public sealed record HostingOpenApiQuery : Solid.IQuery<HostingOpenApiResponse>;

/// <summary>Query used to verify NodaTime and polymorphic protobuf fields.</summary>
[GrpcMethod("GetHostingWireTypes")]
[GrpcService("Hosting")]
[ProtoBuf.ProtoContract]
public sealed record HostingWireTypesQuery : Solid.IQuery<HostingWireTypesResponse>;

/// <summary>Response carrying NodaTime and polymorphic protobuf fields.</summary>
[ProtoBuf.ProtoContract]
public sealed record HostingWireTypesResponse
{
    /// <summary>Gets the representative local date.</summary>
    [ProtoBuf.ProtoMember(1)]
    public LocalDate Date { get; init; }

    /// <summary>Gets the representative local date and time.</summary>
    [ProtoBuf.ProtoMember(2)]
    public LocalDateTime DateTime { get; init; }

    /// <summary>Gets the representative shape.</summary>
    [ProtoBuf.ProtoMember(3, IsRequired = true)]
    public HostingShape Shape { get; init; } = new HostingCircle();
}

/// <summary>Discriminator values for synthetic OpenAPI shapes.</summary>
public enum HostingShapeKind
{
    /// <summary>Circle shape.</summary>
    Circle,
}

/// <summary>Base type for synthetic OpenAPI polymorphism.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(HostingCircle), "circle")]
[ProtoBuf.ProtoContract]
[ProtoBuf.ProtoInclude(10, typeof(HostingCircle))]
public abstract record HostingShape;

/// <summary>Circle shape used by the OpenAPI schema.</summary>
[ProtoBuf.ProtoContract]
public sealed record HostingCircle : HostingShape
{
    /// <summary>Gets or sets the circle radius.</summary>
    [System.ComponentModel.DefaultValue(1)]
    [ProtoBuf.ProtoMember(1)]
    public int Radius { get; init; } = 1;
}

/// <summary>Versioned query used to exercise contract lifetime metadata.</summary>
[HttpEndpoint("GET", "/hosting/versioned/{id}", AllowAnonymous = true)]
[GrpcMethod("GetHostingVersioned")]
[GrpcService("Hosting")]
[Versioning(Introduced = 2, Retired = 4)]
[ProtoBuf.ProtoContract]
public sealed record HostingVersionedQuery : Solid.IQuery<HostingResponse>
{
    /// <summary>Gets or sets the route identifier.</summary>
    [HttpRoute]
    [ProtoBuf.ProtoMember(1)]
    public int Id { get; set; }

    /// <summary>Gets or sets the request value.</summary>
    [HttpQuery]
    [ProtoBuf.ProtoMember(2)]
    public string? Value { get; set; }
}
