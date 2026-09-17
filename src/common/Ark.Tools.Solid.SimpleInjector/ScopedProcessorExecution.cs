// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.Tools.Solid.SimpleInjector;

internal static class ScopedProcessorExecution
{
    public static void EnsureSupported(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (container.Options.DefaultScopedLifestyle is null)
        {
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            return;
        }

        _ = _getScopedLifestyle(container);
    }

    public static async Task ExecuteAsync(Container container, Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var scope = _getScopedLifestyle(container).GetCurrentScope(container);
        if (scope is not null)
        {
            await callback().ConfigureAwait(false);
            return;
        }

#pragma warning disable MA0004 // The scope lifetime is bounded by the processor execution.
        await using var createdScope = _beginScope(container);
#pragma warning restore MA0004
        await callback().ConfigureAwait(false);
    }

    public static async Task<TResult> ExecuteAsync<TResult>(Container container, Func<Task<TResult>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var scope = _getScopedLifestyle(container).GetCurrentScope(container);
        if (scope is not null)
        {
            return await callback().ConfigureAwait(false);
        }

#pragma warning disable MA0004 // The scope lifetime is bounded by the processor execution.
        await using var createdScope = _beginScope(container);
#pragma warning restore MA0004
        return await callback().ConfigureAwait(false);
    }

    private static ScopedLifestyle _getScopedLifestyle(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);

        return container.Options.DefaultScopedLifestyle
            ?? throw new InvalidOperationException("DefaultScopedLifestyle must be configured.");
    }

    private static Scope _beginScope(Container container)
    {
        var lifestyle = _getScopedLifestyle(container);
        var scope = new Scope(container);
        lifestyle.SetCurrentScope(scope);
        return scope;
    }
}
