// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Config;
using Ark.ResourceWatcher.Sample.Tests.Mocks;
using Ark.Tools.ResourceWatcher.Testing;

using NodaTime;


namespace Ark.ResourceWatcher.Sample.Tests.Hooks;

/// <summary>
/// Shared test context for BlobWorkerHost tests.
/// </summary>
public sealed class BlobTestContext : IDisposable
{
    /// <summary>
    /// Gets or sets the mock blob storage API.
    /// </summary>
    public MockProviderApi ProviderApi { get; } = new();

    /// <summary>
    /// Gets or sets the mock sink API.
    /// </summary>
    public MockSinkApi SinkApi { get; } = new();

    /// <summary>
    /// Gets or sets the testable state provider.
    /// </summary>
    public TestableStateProvider StateProvider { get; } = new();

    /// <summary>
    /// Gets or sets the testing diagnostic listener.
    /// </summary>
    public TestingDiagnosticListener DiagnosticListener { get; } = new();

    /// <summary>
    /// Gets or sets the worker configuration.
    /// </summary>
    public MyWorkerHostConfig Config { get; set; } = new()
    {
        WorkerName = "TestBlobWorker",
        DegreeOfParallelism = 1,
        Sleep = TimeSpan.FromSeconds(1),
        MaxRetries = 3,
        BanDuration = Duration.FromMinutes(10),
        IgnoreState = false,
        ProviderUrl = new Uri("http://localhost:10000"),
        SinkUrl = new Uri("http://localhost:20000")
    };

    /// <summary>
    /// Gets the clock for testing.
    /// </summary>
    public IClock Clock { get; } = SystemClock.Instance;

    /// <summary>
    /// Gets the last processing results.
    /// </summary>
    public IReadOnlyDictionary<string, ResourceProcessingResult> LastResults => DiagnosticListener.Results;

    /// <summary>
    /// Disposes the test context.
    /// </summary>
    public void Dispose()
    {
        ProviderApi.Reset();
        SinkApi.Reset();
        StateProvider.ClearAll();
        DiagnosticListener.Clear();
    }
}