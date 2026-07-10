// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.Solid;

using Microsoft.AspNetCore.Mvc;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>Exposes the greeting handler through ASP.NET Core MessagePack formatters.</summary>
[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/v1/messagepack/greetings")]
public sealed class MessagePackGreetingController : ControllerBase
{
    private readonly Container _container;

    /// <summary>Initializes a new instance of the <see cref="MessagePackGreetingController"/> class.</summary>
    /// <param name="container">The application container.</param>
    public MessagePackGreetingController(Container container)
    {
        _container = container;
    }

    /// <summary>Creates a greeting from a MessagePack or JSON request.</summary>
    /// <param name="request">The greeting request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created greeting.</returns>
    [HttpPost]
    [Produces("application/x-msgpack", "application/json")]
    [Consumes("application/x-msgpack", "application/json")]
    public async Task<ActionResult<GreetingResponse>> CreateAsync(
        CreateGreetingRequest request,
        CancellationToken cancellationToken)
    {
        var handler = _container.GetInstance<IRequestHandler<CreateGreetingRequest, GreetingResponse>>();
        var result = await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
