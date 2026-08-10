// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;

namespace Ark.MediatorFramework.Sample.Tests.Fakes;

/// <summary>Proxies an external service through the active scenario binding.</summary>
internal sealed class ScenarioPrintCompletedNotificationService : IPrintCompletedNotificationService
{
    private readonly ScenarioBindingHolder<IPrintCompletedNotificationService> _holder;

    /// <summary>Initializes a proxy for the supplied scenario binding holder.</summary>
    /// <param name="holder">The scenario binding holder.</param>
    public ScenarioPrintCompletedNotificationService(
        ScenarioBindingHolder<IPrintCompletedNotificationService> holder)
    {
        _holder = holder;
    }

    /// <inheritdoc />
    public async Task NotifyAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
    {
        await _holder.Resolve().NotifyAsync(process, ctk).ConfigureAwait(false);
    }
}
