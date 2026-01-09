// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;

using Polly;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.Core(net10.0)', Before:
namespace Ark.Tools.FtpClient.Core
{
    public abstract class FtpClientBase : IFtpClient
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Uri Uri { get; }
        public NetworkCredential Credentials { get; }
        public int MaxListingRecursiveParallelism { get; }

        public FtpConfig FtpConfig { get; }

        protected FtpClientBase(FtpConfig ftpConfig)
            : this(ftpConfig, 3)
        {
        }

        protected FtpClientBase(FtpConfig ftpConfig, int maxListingRecursiveParallelism)
        {
            ArgumentNullException.ThrowIfNull(ftpConfig);
            ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
            ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

            Uri = ftpConfig.Uri;
            Credentials = ftpConfig.Credentials;
            MaxListingRecursiveParallelism = maxListingRecursiveParallelism;

            FtpConfig = ftpConfig;
        }

        public abstract Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default);
        public abstract Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default);
        public abstract Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default);

        public virtual async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "List files starting from path: {Path}", startPath);
            startPath ??= "./";

            if (skipFolder == null)
                skipFolder = x => false;

            Stack<FtpEntry> pendingFolders = new();
            IEnumerable<FtpEntry> files = new List<FtpEntry>();
            List<Task<IEnumerable<FtpEntry>>> running = new();

            async Task<IEnumerable<FtpEntry>> listFolderAsync(string path, CancellationToken ct)
            {
                var retrier = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                    [
                        TimeSpan.FromSeconds(1),
                    ], (ex, ts) =>
                    {
                        _logger.Warn(ex, CultureInfo.InvariantCulture, "Failed to list folder {Path}. Try again in {Sleep} ...", path, ts);
                    });

                return await retrier.ExecuteAsync(async ct1 =>
                {
                    return await ListDirectoryAsync(path, ct1).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);


            }

            void startListing(string path)
            {
                running.Add(Task.Run(() => listFolderAsync(path, ctk), ctk));
            }

            startListing(startPath);

            try
            {
                while (running.Count > 0)
                {
                    var t = await Task.WhenAny(running).ConfigureAwait(false);

                    var list = await t.ConfigureAwait(false);
                    running.Remove(t); // remove only if successful

                    foreach (var d in list.Where(x => x.IsDirectory && !x.Name.Equals(".", StringComparison.Ordinal) && !x.Name.Equals("..", StringComparison.Ordinal)))
                    {
                        if (skipFolder.Invoke(d))
                            _logger.Info(CultureInfo.InvariantCulture, "Skipping folder: {Path}", d.FullPath);
                        else
                            pendingFolders.Push(d);
                    }

                    files = files.Concat(list.Where(x => !x.IsDirectory).ToList());

                    while (pendingFolders.Count > 0 && running.Count < this.MaxListingRecursiveParallelism)
                        startListing(pendingFolders.Pop().FullPath);
                }
            }
            catch
            {
                await Task.WhenAll(running).ConfigureAwait(false); // this still contains the failed one 
                throw;
            }


            return files;
        }

        public abstract Task DeleteFileAsync(string path, CancellationToken ctk = default);
        public abstract Task DeleteDirectoryAsync(string path, CancellationToken ctk = default);
    }


=======
namespace Ark.Tools.FtpClient.Core;

public abstract class FtpClientBase : IFtpClient
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public Uri Uri { get; }
    public NetworkCredential Credentials { get; }
    public int MaxListingRecursiveParallelism { get; }

    public FtpConfig FtpConfig { get; }

    protected FtpClientBase(FtpConfig ftpConfig)
        : this(ftpConfig, 3)
    {
    }

    protected FtpClientBase(FtpConfig ftpConfig, int maxListingRecursiveParallelism)
    {
        ArgumentNullException.ThrowIfNull(ftpConfig);
        ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
        ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

        Uri = ftpConfig.Uri;
        Credentials = ftpConfig.Credentials;
        MaxListingRecursiveParallelism = maxListingRecursiveParallelism;

        FtpConfig = ftpConfig;
    }

    public abstract Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default);
    public abstract Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default);
    public abstract Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default);

    public virtual async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "List files starting from path: {Path}", startPath);
        startPath ??= "./";

        if (skipFolder == null)
            skipFolder = x => false;

        Stack<FtpEntry> pendingFolders = new();
        IEnumerable<FtpEntry> files = new List<FtpEntry>();
        List<Task<IEnumerable<FtpEntry>>> running = new();

        async Task<IEnumerable<FtpEntry>> listFolderAsync(string path, CancellationToken ct)
        {
            var retrier = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                [
                    TimeSpan.FromSeconds(1),
                ], (ex, ts) =>
                {
                    _logger.Warn(ex, CultureInfo.InvariantCulture, "Failed to list folder {Path}. Try again in {Sleep} ...", path, ts);
                });

            return await retrier.ExecuteAsync(async ct1 =>
            {
                return await ListDirectoryAsync(path, ct1).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);


        }

        void startListing(string path)
        {
            running.Add(Task.Run(() => listFolderAsync(path, ctk), ctk));
        }

        startListing(startPath);

        try
        {
            while (running.Count > 0)
            {
                var t = await Task.WhenAny(running).ConfigureAwait(false);

                var list = await t.ConfigureAwait(false);
                running.Remove(t); // remove only if successful

                foreach (var d in list.Where(x => x.IsDirectory && !x.Name.Equals(".", StringComparison.Ordinal) && !x.Name.Equals("..", StringComparison.Ordinal)))
                {
                    if (skipFolder.Invoke(d))
                        _logger.Info(CultureInfo.InvariantCulture, "Skipping folder: {Path}", d.FullPath);
                    else
                        pendingFolders.Push(d);
                }

                files = files.Concat(list.Where(x => !x.IsDirectory).ToList());

                while (pendingFolders.Count > 0 && running.Count < this.MaxListingRecursiveParallelism)
                    startListing(pendingFolders.Pop().FullPath);
            }
        }
        catch
        {
            await Task.WhenAll(running).ConfigureAwait(false); // this still contains the failed one 
            throw;
        }


        return files;
    }

    public abstract Task DeleteFileAsync(string path, CancellationToken ctk = default);
    public abstract Task DeleteDirectoryAsync(string path, CancellationToken ctk = default);
>>>>>>> After
    namespace Ark.Tools.FtpClient.Core;

    public abstract class FtpClientBase : IFtpClient
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Uri Uri { get; }
        public NetworkCredential Credentials { get; }
        public int MaxListingRecursiveParallelism { get; }

        public FtpConfig FtpConfig { get; }

        protected FtpClientBase(FtpConfig ftpConfig)
            : this(ftpConfig, 3)
        {
        }

        protected FtpClientBase(FtpConfig ftpConfig, int maxListingRecursiveParallelism)
        {
            ArgumentNullException.ThrowIfNull(ftpConfig);
            ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
            ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

            Uri = ftpConfig.Uri;
            Credentials = ftpConfig.Credentials;
            MaxListingRecursiveParallelism = maxListingRecursiveParallelism;

            FtpConfig = ftpConfig;
        }

        public abstract Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default);
        public abstract Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default);
        public abstract Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default);

        public virtual async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default)
        {
            _logger.Trace(CultureInfo.InvariantCulture, "List files starting from path: {Path}", startPath);
            startPath ??= "./";

            if (skipFolder == null)
                skipFolder = x => false;

            Stack<FtpEntry> pendingFolders = new();
            IEnumerable<FtpEntry> files = new List<FtpEntry>();
            List<Task<IEnumerable<FtpEntry>>> running = new();

            async Task<IEnumerable<FtpEntry>> listFolderAsync(string path, CancellationToken ct)
            {
                var retrier = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                    [
                        TimeSpan.FromSeconds(1),
                    ], (ex, ts) =>
                    {
                        _logger.Warn(ex, CultureInfo.InvariantCulture, "Failed to list folder {Path}. Try again in {Sleep} ...", path, ts);
                    });

                return await retrier.ExecuteAsync(async ct1 =>
                {
                    return await ListDirectoryAsync(path, ct1).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);


            }

            void startListing(string path)
            {
                running.Add(Task.Run(() => listFolderAsync(path, ctk), ctk));
            }

            startListing(startPath);

            try
            {
                while (running.Count > 0)
                {
                    var t = await Task.WhenAny(running).ConfigureAwait(false);

                    var list = await t.ConfigureAwait(false);
                    running.Remove(t); // remove only if successful

                    foreach (var d in list.Where(x => x.IsDirectory && !x.Name.Equals(".", StringComparison.Ordinal) && !x.Name.Equals("..", StringComparison.Ordinal)))
                    {
                        if (skipFolder.Invoke(d))
                            _logger.Info(CultureInfo.InvariantCulture, "Skipping folder: {Path}", d.FullPath);
                        else
                            pendingFolders.Push(d);
                    }

                    files = files.Concat(list.Where(x => !x.IsDirectory).ToList());

                    while (pendingFolders.Count > 0 && running.Count < this.MaxListingRecursiveParallelism)
                        startListing(pendingFolders.Pop().FullPath);
                }
            }
            catch
            {
                await Task.WhenAll(running).ConfigureAwait(false); // this still contains the failed one 
                throw;
            }


            return files;
        }

        public abstract Task DeleteFileAsync(string path, CancellationToken ctk = default);
        public abstract Task DeleteDirectoryAsync(string path, CancellationToken ctk = default);
    }