// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

namespace Ark.Tools.MediatorFramework;

/// <summary>Outbox helpers for <see cref="IBus"/>, mirroring <c>Ark.Tools.Outbox.Rebus</c>'s <c>bus.Enlist(context)</c>.</summary>
public static class BusOutboxExtensions
{
    /// <summary>Opens an <see cref="OutboxScope"/> over <paramref name="context"/> for the sends that follow.</summary>
    /// <remarks>
    /// Same as <c>new OutboxScope(context)</c>: the scope is ambient, so it stages the sends of every bus, not only
    /// <paramref name="bus"/>. Complete it before committing the application transaction.
    /// </remarks>
    /// <param name="bus">The bus the handler sends through.</param>
    /// <param name="context">The application outbox context, part of the application transaction.</param>
    /// <returns>The opened scope.</returns>
    public static OutboxScope Enlist(this IBus bus, IOutboxContextCore context)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return new OutboxScope(context);
    }
}
