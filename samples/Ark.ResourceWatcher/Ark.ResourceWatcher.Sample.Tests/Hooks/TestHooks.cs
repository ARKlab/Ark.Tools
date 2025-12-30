// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Reqnroll.BoDi;

namespace Ark.ResourceWatcher.Sample.Tests.Hooks;

/// <summary>
/// Reqnroll hooks for test setup and teardown.
/// </summary>
[Binding]
public sealed class TestHooks : IDisposable
{
    private readonly IObjectContainer _container;
    private BlobTestContext? _context;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHooks"/> class.
    /// </summary>
    /// <param name="container">The dependency injection container.</param>
    public TestHooks(IObjectContainer container)
    {
        _container = container;
    }

    /// <summary>
    /// Runs before each scenario to set up shared context.
    /// </summary>
    [BeforeScenario]
    public void BeforeScenario()
    {
        _context = new BlobTestContext();
        _container.RegisterInstanceAs(_context);
    }

    /// <summary>
    /// Runs after each scenario to clean up resources.
    /// </summary>
    [AfterScenario]
    public void AfterScenario()
    {
        Dispose();
    }

    /// <summary>
    /// Disposes the test context.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _context?.Dispose();
            _context = null;
            _disposed = true;
        }
    }
}
