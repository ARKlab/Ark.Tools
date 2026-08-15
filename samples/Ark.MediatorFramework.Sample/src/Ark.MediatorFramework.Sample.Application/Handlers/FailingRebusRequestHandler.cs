// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Pure handler for <see cref="FailingRebusRequest"/> that always throws to force dead-lettering.</summary>
public sealed class FailingRebusRequestHandler : IRequestHandler<FailingRebusRequest, DeadLetterAck>
{
    /// <inheritdoc />
    public Task<DeadLetterAck> ExecuteAsync(FailingRebusRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);
        throw new InvalidOperationException(Request.Reason);
    }
}
