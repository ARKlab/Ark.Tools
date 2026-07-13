// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Opt-in declaration that exposes a pure <c>Ark.Tools.Solid</c> request/query as an HTTP
/// (Minimal API) endpoint.
/// </summary>
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
/// Marks a request property that must be read from the query string when the
/// endpoint also has a request body.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class BindFromQueryAttribute : Attribute
{
}
