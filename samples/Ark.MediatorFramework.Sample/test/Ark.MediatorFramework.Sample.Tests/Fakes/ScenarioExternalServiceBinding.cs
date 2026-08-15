// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.Tests.Fakes;

/// <summary>Holds an external binding explicitly attached to one test scenario.</summary>
internal sealed class ScenarioBindingHolder<TService>
    where TService : class
{
    private TService? _service;

    /// <summary>Attaches the scenario's external binding.</summary>
    /// <param name="service">The binding to attach.</param>
    public void Attach(TService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (Interlocked.CompareExchange(ref _service, service, null) is not null)
            throw new InvalidOperationException("The scenario already has an external binding.");
    }

    /// <summary>Detaches the scenario's external binding.</summary>
    public void Detach()
    {
        Interlocked.Exchange(ref _service, null);
    }

    /// <summary>Resolves the active external binding.</summary>
    /// <returns>The active binding.</returns>
    /// <exception cref="InvalidOperationException">No scenario is active.</exception>
    public TService Resolve()
    {
        return Volatile.Read(ref _service)
            ?? throw new InvalidOperationException("An external service was called outside an active scenario.");
    }
}
