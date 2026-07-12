// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Opt-in declaration that exposes a pure <c>Ark.Tools.Solid</c> request/query as an HTTP
/// (Minimal API) endpoint. The incremental source generator emits a Minimal API mapping only
/// for types carrying this attribute.
/// </summary>
/// <remarks>
/// The decorated type must implement <c>Ark.Tools.Solid.IRequest&lt;T&gt;</c> or
/// <c>Ark.Tools.Solid.IQuery&lt;T&gt;</c>. Transport declaration is explicit and per-transport:
/// omit the attribute (and hand-write the <c>Map*</c> call) when the framework is too limited for a
/// given request/query. The handler stays transport-agnostic; only this attribute expresses the
/// HTTP binding.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HttpEndpointAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="HttpEndpointAttribute"/> class.</summary>
    /// <param name="verb">The HTTP verb, for example <c>GET</c> or <c>POST</c>.</param>
    /// <param name="template">The route template, for example <c>/api/v{version}/greetings</c>.</param>
    public HttpEndpointAttribute(string verb, string template)
    {
        Verb = verb;
        Template = template;
    }

    /// <summary>Gets the HTTP verb.</summary>
    public string Verb { get; }

    /// <summary>Gets the route template.</summary>
    public string Template { get; }

    /// <summary>Gets or sets the first API version in which the endpoint is available.</summary>
    public int IntroducedIn { get; set; } = 1;

    /// <summary>Gets or sets the exclusive API version in which the endpoint is retired, or zero if never.</summary>
    public int RetiredIn { get; set; }
}

/// <summary>
/// Opt-in declaration that exposes a pure <c>Ark.Tools.Solid</c> request as a Rebus message,
/// so the incremental source generator emits an <c>IHandleMessages&lt;T&gt;</c> wrapper for it.
/// </summary>
/// <remarks>
/// Rebus emission is opt-in and independent from <see cref="HttpEndpointAttribute"/>. Omit the
/// attribute (and hand-write the <c>IHandleMessages&lt;T&gt;</c>) when a message needs custom
/// handling the framework does not generate.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RebusMessageAttribute : Attribute
{
}

/// <summary>
/// Opt-in declaration that exposes a pure <c>Ark.Tools.Solid</c> request/query as a code-first
/// gRPC method. Transport declaration is explicit: the generator emits the gRPC service method only
/// for types carrying this attribute.
/// </summary>
/// <remarks>
/// gRPC emission is opt-in and independent from the other transports. Omit the attribute (and
/// hand-write the method within the generated service partial) when the framework cannot express a
/// given gRPC method.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GrpcMethodAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="GrpcMethodAttribute"/> class.</summary>
    /// <param name="name">Optional explicit method name; defaults to the contract type name.</param>
    public GrpcMethodAttribute(string? name = null)
    {
        Name = name;
    }

    /// <summary>Gets the explicit gRPC method name, or <see langword="null"/> to use the type name.</summary>
    public string? Name { get; }

    /// <summary>Gets or sets the first API version in which the method is available.</summary>
    public int IntroducedIn { get; set; } = 1;

    /// <summary>Gets or sets the exclusive API version in which the method is retired, or zero if never.</summary>
    public int RetiredIn { get; set; }
}

/// <summary>Assigns opt-in gRPC methods to a generated code-first service.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ServiceGroupAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ServiceGroupAttribute"/> class.</summary>
    /// <param name="name">The generated service group name.</param>
    public ServiceGroupAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Gets the generated service group name.</summary>
    public string Name { get; }
}
