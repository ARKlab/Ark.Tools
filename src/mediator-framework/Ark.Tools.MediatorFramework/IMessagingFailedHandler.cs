// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Handles an inline second-level failure without persisting a failure message.</summary>
/// <typeparam name="T">The original message type.</typeparam>
public interface IMessagingFailedHandler<T>
    where T : class
{
    /// <summary>Handles the failed original message.</summary>
    /// <param name="failure">The bounded failure snapshot.</param>
    /// <param name="cancellationToken">The delivery cancellation token.</param>
    /// <returns>A task that completes when the failure is handled.</returns>
    Task HandleAsync(IFailed<T> failure, CancellationToken cancellationToken);
}
