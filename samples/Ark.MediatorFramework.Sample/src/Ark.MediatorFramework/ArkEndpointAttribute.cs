// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Marks a pure <c>Ark.Tools.Solid</c> request/query as an HTTP endpoint so the
/// incremental source generator can emit a Minimal API mapping for it.
/// </summary>
/// <remarks>
/// The decorated type must implement <c>Ark.Tools.Solid.IRequest&lt;T&gt;</c> or
/// <c>Ark.Tools.Solid.IQuery&lt;T&gt;</c>. The handler stays transport-agnostic;
/// only this attribute expresses the HTTP binding.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ArkEndpointAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ArkEndpointAttribute"/> class.</summary>
    /// <param name="verb">The HTTP verb, for example <c>GET</c> or <c>POST</c>.</param>
    /// <param name="template">The route template, for example <c>/api/v1/greetings</c>.</param>
    public ArkEndpointAttribute(string verb, string template)
    {
        Verb = verb;
        Template = template;
    }

    /// <summary>Gets the HTTP verb.</summary>
    public string Verb { get; }

    /// <summary>Gets the route template.</summary>
    public string Template { get; }
}
