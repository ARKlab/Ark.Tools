// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;
using Ark.Tools.FtpClient.Core;

using AwesomeAssertions;

using Renci.SshNet.Common;

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Ark.Tools.FtpClient.SftpClient.Tests;

/// <summary>
/// SFTP transfers, including cancellation while the server has stopped answering. The round-trip and mid-transfer
/// tests need a real SFTP server and are skipped without one; the never-answers test is self-contained.
/// Set ARK_SFTP_TEST_URI to sftp://user:password@host:port/writable-folder, e.g. for the sftp service of the root
/// docker-compose.yml (<c>docker compose up -d sftp</c>, as in CI) use sftp://ark:ark@127.0.0.1:2222/upload.
/// </summary>
[TestClass]
public class SftpClientConnectionTests
{
    private const int _fileSize = 16 * 1024 * 1024;

    // Far below the 5 minute SSH.NET OperationTimeout that a stalled transfer used to wait for after cancellation.
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the MSTest context.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UploadThenDownloadFileAsync_ShouldRoundTripTheContentAndStayConnected()
    {
        var server = _getServer();
        var path = $"{server.Folder}/{Guid.NewGuid():N}.bin";
        var content = new byte[_fileSize];
        RandomNumberGenerator.Fill(content);
        using var serverConfig = server.CreateConfig(server.Endpoint);
        using var connection = new SftpClientConnection(serverConfig);
        try
        {
            await connection.UploadFileAsync(path, content, TestContext.CancellationToken).ConfigureAwait(false);
            var downloaded = await connection.DownloadFileAsync(path, TestContext.CancellationToken).ConfigureAwait(false);

            downloaded.Should().Equal(content);
            (await connection.IsConnectedAsync(TestContext.CancellationToken).ConfigureAwait(false)).Should().BeTrue();
        }
        finally
        {
            await _deleteRemoteFileAsync(server, path).ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task DownloadFileAsync_ServerNeverAnswersThenTokenIsCancelled_ShouldStop()
    {
        // Accepts the TCP connection but never sends the SSH identification, like a server hung before the handshake.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var config = new FtpConfig(new Uri($"sftp://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}"), new NetworkCredential("user", "placeholder"));
        using var connection = new SftpClientConnection(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var act = async () => await connection.DownloadFileAsync("file.bin", cts.Token).WaitAsync(_bound, TestContext.CancellationToken).ConfigureAwait(false);

        var thrown = await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
        thrown.Which.CancellationToken.Should().Be(cts.Token);
    }

    [TestMethod]
    public async Task DownloadFileAsync_ServerStallsThenTokenIsCancelled_ShouldStopAndDisconnect()
    {
        var server = _getServer();
        var path = $"{server.Folder}/{Guid.NewGuid():N}.bin";
        try
        {
            using (var serverConfig = server.CreateConfig(server.Endpoint))
            using (var setup = new SftpClientConnection(serverConfig))
            {
                await setup.UploadFileAsync(path, new byte[_fileSize], TestContext.CancellationToken).ConfigureAwait(false);
            }

            var proxy = StallingProxy.Start(server.Endpoint);
            await using var proxyScope = proxy.ConfigureAwait(false);
            using var proxyConfig = server.CreateConfig(proxy.Endpoint);
            using var connection = new SftpClientConnection(proxyConfig);
            using var cts = new CancellationTokenSource();

            var download = connection.DownloadFileAsync(path, cts.Token);
            await proxy.Stalled.WaitAsync(_bound, TestContext.CancellationToken).ConfigureAwait(false);
            await cts.CancelAsync().ConfigureAwait(false);

            var act = async () => await download.WaitAsync(_bound, TestContext.CancellationToken).ConfigureAwait(false);
            await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
            (await connection.IsConnectedAsync(TestContext.CancellationToken).ConfigureAwait(false)).Should().BeFalse();
        }
        finally
        {
            await _deleteRemoteFileAsync(server, path).ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task PoolUploadFileAsync_ServerStallsThenTokenIsCancelled_ShouldStopAndNotReuseTheConnection()
    {
        var server = _getServer();
        var path = $"{server.Folder}/{Guid.NewGuid():N}.bin";
        try
        {
            var proxy = StallingProxy.Start(server.Endpoint);
            await using var proxyScope = proxy.ConfigureAwait(false);
            using var proxyConfig = server.CreateConfig(proxy.Endpoint);
            using var pool = new FtpClientPool(1, proxyConfig, new SFtpClientConnectionFactory());
            using var cts = new CancellationTokenSource();

            var upload = pool.UploadFileAsync(path, new byte[_fileSize], cts.Token);
            await proxy.Stalled.WaitAsync(_bound, TestContext.CancellationToken).ConfigureAwait(false);
            await cts.CancelAsync().ConfigureAwait(false);

            var act = async () => await upload.WaitAsync(_bound, TestContext.CancellationToken).ConfigureAwait(false);
            await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);

            using var listCts = new CancellationTokenSource(_bound);
            var entries = await pool.ListDirectoryAsync(server.Folder, listCts.Token).ConfigureAwait(false);
            entries.Should().NotBeNull();
            proxy.Connections.Should().Be(2, "the pool must drop the connection of the cancelled upload and open a new one");
        }
        finally
        {
            // the cancelled upload may have left a partial file
            await _deleteRemoteFileAsync(server, path).ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task PoolListFilesRecursiveAsync_ShouldListEveryFileOnSeveralConnections()
    {
        var server = _getServer();
        var root = $"{server.Folder}/{Guid.NewGuid():N}";
        var folders = Enumerable.Range(0, 6).Select(i => $"{root}/d{i}").ToList();
        var files = folders.Select(static f => $"{f}/sub/file.txt").ToList();
        using var sftp = new Renci.SshNet.SftpClient(server.Endpoint.Host, server.Endpoint.Port, server.Credentials.UserName, server.Credentials.Password);
        await sftp.ConnectAsync(TestContext.CancellationToken).ConfigureAwait(false);
        try
        {
            await sftp.CreateDirectoryAsync(root, TestContext.CancellationToken).ConfigureAwait(false);
            foreach (var folder in folders)
            {
                await sftp.CreateDirectoryAsync(folder, TestContext.CancellationToken).ConfigureAwait(false);
                await sftp.CreateDirectoryAsync($"{folder}/sub", TestContext.CancellationToken).ConfigureAwait(false);
                using var content = new MemoryStream([1]);
                await sftp.UploadFileAsync(content, $"{folder}/sub/file.txt", TestContext.CancellationToken).ConfigureAwait(false);
            }

            // only counts connections: listings stay far below the 1 MiB after which the first connection stalls
            var proxy = StallingProxy.Start(server.Endpoint);
            await using var proxyScope = proxy.ConfigureAwait(false);
            using var proxyConfig = server.CreateConfig(proxy.Endpoint);
            using var pool = new FtpClientPool(3, proxyConfig, new SFtpClientConnectionFactory());
            using var listCts = new CancellationTokenSource(_bound);

            var entries = await pool.ListFilesRecursiveAsync(root, ctk: listCts.Token).ConfigureAwait(false);

            entries.Select(static e => e.FullPath).Should().BeEquivalentTo(files);
            proxy.Connections.Should().BeGreaterThan(1, "the pool must list folders in parallel on its connections");
        }
        finally
        {
            // the setup may have stopped before creating every path
            var existingFiles = files.ToAsyncEnumerable().Where(async (f, ct) => await sftp.ExistsAsync(f, ct).ConfigureAwait(false));
            await foreach (var file in existingFiles.WithCancellation(TestContext.CancellationToken).ConfigureAwait(false))
            {
                await sftp.DeleteFileAsync(file, TestContext.CancellationToken).ConfigureAwait(false);
            }

            var existingFolders = folders.SelectMany(static f => new[] { $"{f}/sub", f }).Append(root).ToAsyncEnumerable().Where(async (f, ct) => await sftp.ExistsAsync(f, ct).ConfigureAwait(false));
            await foreach (var folder in existingFolders.WithCancellation(TestContext.CancellationToken).ConfigureAwait(false))
            {
                await sftp.DeleteDirectoryAsync(folder, TestContext.CancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Uses its own connection: the test's connection may have been disconnected by the cancellation under test.
    private static async Task _deleteRemoteFileAsync(SftpServer server, string path)
    {
        using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var config = server.CreateConfig(server.Endpoint);
        using var connection = new SftpClientConnection(config);
        try
        {
            await connection.DeleteFileAsync(path, cleanupCts.Token).ConfigureAwait(false);
        }
        catch (SftpPathNotFoundException)
        {
            // the transfer never created the file
        }
    }

    private static SftpServer _getServer()
    {
        var value = Environment.GetEnvironmentVariable("ARK_SFTP_TEST_URI");
        if (string.IsNullOrWhiteSpace(value))
            Assert.Inconclusive("Set ARK_SFTP_TEST_URI to sftp://user:password@host:port/writable-folder to run the SFTP tests.");

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new SftpServer(
            new DnsEndPoint(uri.Host, uri.Port),
            new NetworkCredential(Uri.UnescapeDataString(userInfo[0]), Uri.UnescapeDataString(userInfo[1])),
            uri.AbsolutePath.TrimEnd('/'));
    }

    private sealed record SftpServer(DnsEndPoint Endpoint, [property: Secret] NetworkCredential Credentials, string Folder)
    {
        public FtpConfig CreateConfig(DnsEndPoint endpoint)
        {
            return new FtpConfig(new Uri($"sftp://{endpoint.Host}:{endpoint.Port}"), Credentials);
        }
    }

    // Relays TCP to the SFTP server. The first connection stops relaying the server's data once 1 MiB has crossed it,
    // like a server that hangs mid-transfer without closing the socket; later connections are relayed normally.
    private sealed class StallingProxy : IAsyncDisposable
    {
        private const long _stallAfterBytes = 1024 * 1024;

        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly TaskCompletionSource _stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<TcpClient> _sockets = [];
        private readonly List<Task> _pumps = [];
        private readonly DnsEndPoint _server;
        private Task _acceptLoop = Task.CompletedTask;
        private long _relayedBytes;
        private int _connections;

        private StallingProxy(DnsEndPoint server)
        {
            _server = server;
        }

        public Task Stalled => _stalled.Task;

        public int Connections => Volatile.Read(ref _connections);

        public DnsEndPoint Endpoint => new("127.0.0.1", ((IPEndPoint)_listener.LocalEndpoint).Port);

        public static StallingProxy Start(DnsEndPoint server)
        {
            var proxy = new StallingProxy(server);
            proxy._listener.Start();
            proxy._acceptLoop = proxy._acceptAsync();
            return proxy;
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _listener.Dispose();
            lock (_sockets)
            {
                foreach (var socket in _sockets)
                    socket.Dispose();
            }

            await _acceptLoop.ConfigureAwait(false);
            Task[] pumps;
            lock (_pumps)
                pumps = [.. _pumps];
            await Task.WhenAll(pumps).ConfigureAwait(false);
            _cts.Dispose();
        }

        private async Task _acceptAsync()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                    var upstream = new TcpClient();
                    lock (_sockets)
                    {
                        _sockets.Add(client);
                        _sockets.Add(upstream);
                    }

                    await upstream.ConnectAsync(_server.Host, _server.Port, _cts.Token).ConfigureAwait(false);
                    var stallable = Interlocked.Increment(ref _connections) == 1;
                    lock (_pumps)
                    {
                        _pumps.Add(_pumpAsync(client.GetStream(), upstream.GetStream(), stallable, stall: false));
                        _pumps.Add(_pumpAsync(upstream.GetStream(), client.GetStream(), stallable, stall: stallable));
                    }
                }
            }
            catch (Exception ex) when (_isShutdown(ex))
            {
                // the proxy is being disposed
            }
        }

        private async Task _pumpAsync(NetworkStream from, NetworkStream to, bool count, bool stall)
        {
            var buffer = new byte[81920];
            try
            {
                int read;
                while ((read = await from.ReadAsync(buffer, _cts.Token).ConfigureAwait(false)) > 0)
                {
                    var relayed = count ? Interlocked.Add(ref _relayedBytes, read) : 0;
                    if (stall && relayed > _stallAfterBytes)
                    {
                        // Stop reading the server and keep both sockets open: the client sees a silent server.
                        _stalled.TrySetResult();
                        return;
                    }

                    await to.WriteAsync(buffer.AsMemory(0, read), _cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (_isShutdown(ex))
            {
                // a socket was closed by the client, the server or the proxy disposal
            }
        }

        private static bool _isShutdown(Exception ex)
        {
            return ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException;
        }
    }
}
