// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework;
using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Aot.Application;

/// <summary>A greeting.</summary>
public sealed record Greeting
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the greeted name.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>Creates a greeting and sends it to the processor.</summary>
public static class Greeting_CreateRequest
{
    /// <summary>Version one of the greeting creation request.</summary>
    [HttpEndpoint("POST", "/api/v{version}/greetings", AllowAnonymous = true)]
    public sealed record V1 : IRequest<V1, Greeting>
    {
        /// <summary>Gets the name to greet.</summary>
        public string Name { get; init; } = string.Empty;
    }
}

/// <summary>Reads a greeting created by this instance.</summary>
public static class Greeting_GetQuery
{
    /// <summary>Version one of the greeting query.</summary>
    /// <remarks>A <see langword="null"/> result answers 404. The result type is not nullable: the generated function drops the annotation.</remarks>
    [HttpEndpoint("GET", "/api/v{version}/greetings/{id}", AllowAnonymous = true)]
    public sealed record V1 : IQuery<V1, Greeting>
    {
        /// <summary>Gets the greeting identifier.</summary>
        [HttpRoute]
        public Guid Id { get; init; }
    }
}

/// <summary>Asks the processor to process a greeting.</summary>
[Message(Name = "aot/greeting.process")]
public sealed record ProcessGreetingCommand : ICommand<ProcessGreetingCommand>
{
    /// <summary>Gets the greeting.</summary>
    public Greeting Greeting { get; init; } = new();
}
