// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.FtpClient.Core;
using Ark.Tools.ResourceWatcher.WorkerHost;
using Ark.Tools.ResourceWatcher.WorkerHost.Ftp;

using AwesomeAssertions;

using Reqnroll;

using System.Net;

namespace Ark.Tools.ResourceWatcher.Tests.Steps;

/// <summary>
/// Step definitions that verify the FTP connection pool lifetime of <see cref="FtpWorkerHost{TPayload}"/>.
/// </summary>
[Binding]
[Scope(Tag = "ftp-workerhost")]
public sealed class FtpWorkerHostSteps
{
    private readonly PoolTracker _tracker = new();
    private FtpWorkerHost<string>? _workerHost;

    [Given(@"an FtpWorkerHost with a tracking FTP client pool factory")]
    public void GivenAnFtpWorkerHostWithATrackingFtpClientPoolFactory()
    {
        var config = new FtpTestHostConfig
        {
            WorkerName = "FtpWorkerHostTests",
            FtpConfig = new FtpConfig(new Uri("ftp://localhost/")),
        };

        _workerHost = new FtpWorkerHost<string>(config);
        _workerHost.UseStateProvider<InMemStateProvider<FtpResourceExtensions>>();
        _workerHost.UseFtpClientPoolFactory<TrackingPoolFactory>(d => d.Container.RegisterInstance(_tracker));
        _workerHost.UseParser<NoOpParser>();
        _workerHost.AppendFileProcessor<NoOpProcessor>();
    }

    [When(@"I run the FtpWorkerHost (\d+) times")]
    public async Task WhenIRunTheFtpWorkerHostTimes(int runs)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var i = 0; i < runs; i++)
        {
            await _workerHost!.RunOnceAsync(ctk: cts.Token);
        }
    }

    [Then(@"the FTP client pool factory should have created (\d+) pool")]
    public void ThenTheFtpClientPoolFactoryShouldHaveCreatedPool(int expected)
    {
        _tracker.Created.Should().Be(expected);
    }

    [Then(@"no FTP client pool should have been disposed")]
    public void ThenNoFtpClientPoolShouldHaveBeenDisposed()
    {
        _tracker.Disposed.Should().Be(0);
    }

    private sealed class FtpTestHostConfig : DefaultHostConfig, IFtpHostConfig
    {
        public required FtpConfig FtpConfig { get; init; }
        public TimeSpan ListingTimeout { get; init; } = TimeSpan.FromSeconds(10);
        public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromSeconds(10);
        public int MaxConcurrentConnections { get; init; } = 1;
    }

    private sealed class PoolTracker
    {
        public int Created;
        public int Disposed;
    }

    private sealed class TrackingPoolFactory : IFtpClientPoolFactory
    {
        private readonly PoolTracker _tracker;

        public TrackingPoolFactory(PoolTracker tracker)
        {
            _tracker = tracker;
        }

        public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
        {
            Interlocked.Increment(ref _tracker.Created);
            return new EmptyFtpClientPool(_tracker, ftpConfig.Uri);
        }
    }

    private sealed class EmptyFtpClientPool : IFtpClientPool
    {
        private readonly PoolTracker _tracker;

        public EmptyFtpClientPool(PoolTracker tracker, Uri uri)
        {
            _tracker = tracker;
            Uri = uri;
        }

        public Uri Uri { get; }

        public NetworkCredential? Credentials => null;

        public async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default)
        {
            return await Task.FromResult(Enumerable.Empty<FtpEntry>()).ConfigureAwait(false);
        }

        public Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default) => throw new NotSupportedException();

        public Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default) => throw new NotSupportedException();

        public Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default) => throw new NotSupportedException();

        public Task DeleteFileAsync(string path, CancellationToken ctk = default) => throw new NotSupportedException();

        public Task DeleteDirectoryAsync(string path, CancellationToken ctk = default) => throw new NotSupportedException();

        public void Dispose()
        {
            Interlocked.Increment(ref _tracker.Disposed);
        }
    }

    private sealed class NoOpParser : IFtpParser<string>
    {
        public string Parse(FtpMetadata metadata, byte[] contents) => string.Empty;
    }

    private sealed class NoOpProcessor : IResourceProcessor<FtpFile<string>, FtpMetadata, FtpResourceExtensions>
    {
        public async Task Process(FtpFile<string> file, CancellationToken ctk = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
