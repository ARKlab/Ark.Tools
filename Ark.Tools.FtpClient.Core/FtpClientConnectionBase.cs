// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NLog;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.Core
{
    public abstract class FtpClientConnectionBase : IFtpClientConnection
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public string Host { get; }
        public NetworkCredential Credentials { get; }

        protected FtpClientConnectionBase(string host, NetworkCredential credential)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credential);

            Host = host;
            Credentials = credential;
        }

        public abstract Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default);
        public abstract Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default);
        public abstract Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default);        
        public virtual async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = null, Predicate<FtpEntry> skipFolder = null, CancellationToken ctk = default)
        {
            _logger.Trace("List files starting from path: {0}", startPath);

            if (skipFolder == null)
                skipFolder = x => false;

            Stack<FtpEntry> pendingFolders = new Stack<FtpEntry>();
            IEnumerable<FtpEntry> files = new List<FtpEntry>();

            Func<string, CancellationToken, Task> listFolderAsync = async (string path, CancellationToken ct) =>
            {
                var retrier = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(new[]
                    {
                        TimeSpan.FromSeconds(1),
                    }, (ex, ts) =>
                    {
                        _logger.Warn(ex, "Failed to list folder {0}. Try again soon ...", path);
                    });

                var list = await retrier.ExecuteAsync(async ct1 =>
                {
                    return await this.ListDirectoryAsync(path, ct1);
                }, ct);

                foreach (var d in list.Where(x => x.IsDirectory && !x.Name.Equals(".") && !x.Name.Equals("..")))
                {
                    if (skipFolder.Invoke(d))
                        _logger.Info("Skipping folder: {0}", d.FullPath);
                    else
                        pendingFolders.Push(d);
                }

                files = files.Concat(list.Where(x => !x.IsDirectory).ToList());
            };

            await listFolderAsync(startPath, ctk);

            while (pendingFolders.Count > 0)
                await listFolderAsync(pendingFolders.Pop().FullPath, ctk);

            return files;
        }

        public abstract ValueTask ConnectAsync(CancellationToken ctk);
        public abstract ValueTask<bool> IsConnectedAsync(CancellationToken ctk = default);
        public abstract ValueTask DisconnectAsync(CancellationToken ctk = default);
        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            Dispose(true);
        }

    }
}