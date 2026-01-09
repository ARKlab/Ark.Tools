// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;

using Polly;

using System.Globalization;

namespace Ark.Tools.FtpClient.Core;

public abstract class FtpClientWithConnectionBase : FtpClientBase
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    protected FtpClientWithConnectionBase(FtpConfig ftpConfig)
        : base(ftpConfig)
    {
    }

    protected FtpClientWithConnectionBase(FtpConfig ftpConfig, int maxListingRecursiveParallelism)
        : base(ftpConfig, maxListingRecursiveParallelism)
    {
    }

    public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
    {
        using var client = await GetConnection(ctk).ConfigureAwait(false);
        await client.ConnectAsync(ctk).ConfigureAwait(false);
        var ret = await client.DownloadFileAsync(path, ctk).ConfigureAwait(false);
        await client.DisconnectAsync(ctk).ConfigureAwait(false);
        return ret;
    }

    public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default)
    {
        path ??= "./";
        using var client = await GetConnection(ctk).ConfigureAwait(false);
        await client.ConnectAsync(ctk).ConfigureAwait(false);
        var ret = await client.ListDirectoryAsync(path, ctk).ConfigureAwait(false);
        await client.DisconnectAsync(ctk).ConfigureAwait(false);
        return ret;
    }

    public override async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default)
    {
        IFtpClientConnection? conn = null;
        startPath ??= "./";
        try
        {
            _logger.Trace(CultureInfo.InvariantCulture, "List files starting from path: {Path}", startPath);

            conn = await GetConnection(ctk).ConfigureAwait(false);
            await conn.ConnectAsync(ctk).ConfigureAwait(false);

            if (skipFolder == null)
                skipFolder = x => false;

            Stack<FtpEntry> pendingFolders = new();
            IEnumerable<FtpEntry> files = new List<FtpEntry>();

            async Task ListFolderAsync(string path, CancellationToken ct)
            {
                var retrier = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                    [
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(1),
                    ], (ex, ts) =>
                    {
                        _logger.Warn(ex, CultureInfo.InvariantCulture, "Failed to list folder {Path}. Try again in {Sleep} ...", path, ts);
                    });

                var list = await retrier.ExecuteAsync(async ct1 =>
                {
                    if (!await conn.IsConnectedAsync(ctk).ConfigureAwait(false))
                    {
                        conn.Dispose();
                        conn = await GetConnection(ctk).ConfigureAwait(false);
                        await conn.ConnectAsync(ctk).ConfigureAwait(false);
                    }
                    return await conn.ListDirectoryAsync(path, ct1).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);

                foreach (var d in list.Where(x => x.IsDirectory && !x.Name.Equals(".", StringComparison.Ordinal) && !x.Name.Equals("..", StringComparison.Ordinal)))
                {
                    if (skipFolder.Invoke(d))
                        _logger.Info(CultureInfo.InvariantCulture, "Skipping folder: {Path}", d.FullPath);
                    else
                        pendingFolders.Push(d);
                }

                files = files.Concat(list.Where(x => !x.IsDirectory).ToList());
            }

            await ListFolderAsync(startPath, ctk).ConfigureAwait(false);

            while (pendingFolders.Count > 0)
                await ListFolderAsync(pendingFolders.Pop().FullPath, ctk).ConfigureAwait(false);

            return files;
        }
        finally
        {
            conn?.Dispose();
        }
    }

    public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
    {
        using var client = await GetConnection(ctk).ConfigureAwait(false);
        await client.ConnectAsync(ctk).ConfigureAwait(false);
        await client.UploadFileAsync(path, content, ctk).ConfigureAwait(false);
        await client.DisconnectAsync(ctk).ConfigureAwait(false);
    }

    protected abstract Task<IFtpClientConnection> GetConnection(CancellationToken ctk = default);

    public override async Task DeleteFileAsync(string path, CancellationToken ctk = default)
    {
        using var client = await GetConnection(ctk).ConfigureAwait(false);
        await client.ConnectAsync(ctk).ConfigureAwait(false);
        await client.DeleteFileAsync(path, ctk).ConfigureAwait(false);
        await client.DisconnectAsync(ctk).ConfigureAwait(false);
    }

    public override async Task DeleteDirectoryAsync(string path, CancellationToken ctk = default)
    {
        using var client = await GetConnection(ctk).ConfigureAwait(false);
        await client.ConnectAsync(ctk).ConfigureAwait(false);
        await client.DeleteDirectoryAsync(path, ctk).ConfigureAwait(false);
        await client.DisconnectAsync(ctk).ConfigureAwait(false);
    }
}