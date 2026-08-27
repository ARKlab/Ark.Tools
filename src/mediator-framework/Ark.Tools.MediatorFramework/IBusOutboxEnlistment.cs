// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

namespace Ark.Tools.MediatorFramework;

/// <summary>Enlists one-way bus operations in an application outbox transaction.</summary>
public interface IBusOutboxEnlistment
{
    /// <summary>Creates an enlisted bus scope.</summary>
    /// <param name="context">The application outbox context.</param>
    /// <returns>The enlisted scope.</returns>
    IBusOutboxScope Enlist(IOutboxContextCore context);
}

/// <summary>Controls completion of an enlisted bus transaction.</summary>
public interface IBusOutboxScope : IDisposable
{
    /// <summary>Completes the enlisted bus transaction.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes with the transaction.</returns>
    Task CompleteAsync(CancellationToken cancellationToken = default);
}
