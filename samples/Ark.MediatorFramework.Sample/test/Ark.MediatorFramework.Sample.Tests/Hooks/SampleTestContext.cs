// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Reqnroll;

using Ark.MediatorFramework.Sample.Tests.Fakes;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Owns the direct application composition for one Reqnroll scenario.</summary>
[Binding]
public sealed class SampleTestContext : IAsyncDisposable
{
    private ApplicationTestContext? _application;
    private readonly MockPrintCompletedNotificationService _printCompletedNotificationService;

    /// <summary>Initializes the scenario context with its external-service mock binding.</summary>
    /// <param name="printCompletedNotificationService">The scenario-owned print notification mock.</param>
    public SampleTestContext(MockPrintCompletedNotificationService printCompletedNotificationService)
    {
        _printCompletedNotificationService = printCompletedNotificationService;
    }

    /// <summary>Gets the scenario-owned application context.</summary>
    public ApplicationTestContext Application =>
        _application ?? throw new InvalidOperationException("The scenario application is not initialized.");

    internal ApplicationTestContext? ApplicationIfInitialized => _application;

    /// <summary>Sets the integration-test environment before scenarios are created.</summary>
    [BeforeTestRun(Order = HooksOrder.TestInfrastructure)]
    public static void ConfigureEnvironment()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
    }

    /// <summary>Creates the scenario-owned application graph and its resources.</summary>
    [BeforeScenario(Order = HooksOrder.ApplicationSetup)]
    public void CreateApplication()
    {
        _application = new ApplicationTestContext(
            printCompletedNotificationService: _printCompletedNotificationService);
    }

    /// <summary>Disposes the scenario-owned application graph after every scenario.</summary>
    [AfterScenario(Order = HooksOrder.ApplicationCleanup)]
    public async Task DisposeApplication()
    {
        await DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.DisposeAsync().ConfigureAwait(false);
            _application = null;
        }
    }
}
