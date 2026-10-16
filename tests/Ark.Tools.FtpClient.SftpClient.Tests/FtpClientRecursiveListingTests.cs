// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.FtpClient.Core;

using AwesomeAssertions;

using System.Net;

namespace Ark.Tools.FtpClient.SftpClient.Tests;

/// <summary>
/// How many connections <see cref="Core.FtpClient"/> and <see cref="FtpClientPool"/> use to list a tree recursively, against
/// an in-memory server with a root folder of <see cref="_folderCount"/> sub folders holding one file each.
/// </summary>
[TestClass]
public class FtpClientRecursiveListingTests
{
    private const string _root = "/root";
    private const int _folderCount = 6;

    // Only reached when a walk lists fewer folders at once than expected.
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);

    private static readonly string[] _files = Enumerable.Range(0, _folderCount).Select(static i => $"{_root}/d{i}/file.txt").ToArray();

    /// <summary>Gets or sets the MSTest context.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FtpClientListFilesRecursiveAsync_ShouldUseOneConnectionAtATime()
    {
        // a parallel walk would get a second listing in while the first one waits
        var server = new FakeServer(listingsToWaitFor: 2, TimeSpan.FromMilliseconds(100));
        using var config = _createConfig();
        var client = new Core.FtpClient(config, server);

        var entries = await client.ListFilesRecursiveAsync(_root, ctk: TestContext.CancellationToken).ConfigureAwait(false);

        entries.Select(static e => e.FullPath).Should().BeEquivalentTo(_files);
        server.MaxOpenConnections.Should().Be(1);
        server.MaxConcurrentListings.Should().Be(1);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    public async Task FtpClientPoolListFilesRecursiveAsync_ShouldListConcurrentlyUpToPoolMaxSize(int poolMaxSize)
    {
        var server = new FakeServer(listingsToWaitFor: poolMaxSize, _wait);
        using var config = _createConfig();
        using var pool = new FtpClientPool(poolMaxSize, config, server);

        var entries = await pool.ListFilesRecursiveAsync(_root, ctk: TestContext.CancellationToken).ConfigureAwait(false);

        entries.Select(static e => e.FullPath).Should().BeEquivalentTo(_files);
        server.MaxConcurrentListings.Should().Be(poolMaxSize);
        server.MaxOpenConnections.Should().Be(poolMaxSize);
    }

    [TestMethod]
    public async Task FtpClientPoolListFilesRecursiveAsync_ShouldNotListSkippedFolders()
    {
        var server = new FakeServer(listingsToWaitFor: 1, _wait);
        using var config = _createConfig();
        using var pool = new FtpClientPool(3, config, server);

        var entries = await pool.ListFilesRecursiveAsync(_root, static f => f.Name == "d0", TestContext.CancellationToken).ConfigureAwait(false);

        entries.Select(static e => e.FullPath).Should().BeEquivalentTo(_files.Skip(1));
    }

    private static FtpConfig _createConfig()
    {
        return new FtpConfig(new Uri("sftp://ftp.example.com"), new NetworkCredential("user", "placeholder"));
    }

    // Holds each sub folder listing until listingsToWaitFor listings run at once, so a parallel walk reaches its bound
    // deterministically; a walk that never gets there goes on after the wait and fails the concurrency assertion.
    private sealed class FakeServer : IFtpClientConnectionFactory
    {
        private readonly int _listingsToWaitFor;
        private readonly TimeSpan _wait;
        private readonly TaskCompletionSource _enoughListings = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock _lock = new();
        private int _openConnections;
        private int _concurrentListings;

        public FakeServer(int listingsToWaitFor, TimeSpan wait)
        {
            _listingsToWaitFor = listingsToWaitFor;
            _wait = wait;
        }

        public int MaxOpenConnections { get; private set; }
        public int MaxConcurrentListings { get; private set; }

        public IFtpClientConnection Create(FtpConfig ftpConfig)
        {
            lock (_lock)
            {
                MaxOpenConnections = Math.Max(MaxOpenConnections, ++_openConnections);
            }

            return new FakeConnection(this, ftpConfig);
        }

        public async Task<IEnumerable<FtpEntry>> ListAsync(string path, CancellationToken ctk)
        {
            lock (_lock)
            {
                MaxConcurrentListings = Math.Max(MaxConcurrentListings, ++_concurrentListings);
                if (_concurrentListings >= _listingsToWaitFor)
                    _enoughListings.TrySetResult();
            }

            try
            {
                if (path == _root)
                {
                    return Enumerable.Range(0, _folderCount)
                        .Select(static i => new FtpEntry { Name = $"d{i}", FullPath = $"{_root}/d{i}", IsDirectory = true })
                        .Prepend(new FtpEntry { Name = ".", FullPath = _root, IsDirectory = true })
                        .ToList();
                }

                _ = await Task.WhenAny(_enoughListings.Task, Task.Delay(_wait, ctk)).ConfigureAwait(false);
                return [new FtpEntry { Name = "file.txt", FullPath = $"{path}/file.txt", Size = 1 }];
            }
            finally
            {
                lock (_lock)
                {
                    _concurrentListings--;
                }
            }
        }

        public void Closed()
        {
            lock (_lock)
            {
                _openConnections--;
            }
        }
    }

    private sealed class FakeConnection : IFtpClientConnection
    {
        private readonly FakeServer _server;
        private bool _connected;
        private bool _disposed;

        public FakeConnection(FakeServer server, FtpConfig ftpConfig)
        {
            _server = server;
            Uri = ftpConfig.Uri;
            Credentials = ftpConfig.Credentials!;
        }

        public Uri Uri { get; }

        public NetworkCredential Credentials { get; }

        public ValueTask ConnectAsync(CancellationToken ctk)
        {
            _connected = true;
            return default;
        }

        public ValueTask<bool> IsConnectedAsync(CancellationToken ctk = default)
        {
            return ValueTask.FromResult(_connected);
        }

        public ValueTask DisconnectAsync(CancellationToken ctk = default)
        {
            _connected = false;
            return default;
        }

        public async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default)
        {
            return await _server.ListAsync(path, ctk).ConfigureAwait(false);
        }

        public Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteFileAsync(string path, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteDirectoryAsync(string path, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _server.Closed();
        }
    }
}
