// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
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
    public abstract class FtpClientWithConnectionBase : FtpClientBase
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

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
            using (var client = await GetConnection(ctk))
            {
                await client.ConnectAsync(ctk);
                var ret = await client.DownloadFileAsync(path, ctk);
                await client.DisconnectAsync(ctk);
                return ret;
            }
        }

        public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default)
        {
            using (var client = await GetConnection(ctk))
            {
                await client.ConnectAsync(ctk);
                var ret = await client.ListDirectoryAsync(path, ctk);
                await client.DisconnectAsync(ctk);
                return ret;
            }
        }

        public override async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = null, Predicate<FtpEntry> skipFolder = null, CancellationToken ctk = default)
        {
            IFtpClientConnection conn = null;            
            try
            {
                _logger.Trace("List files starting from path: {0}", startPath);

                conn = await GetConnection(ctk);
                await conn.ConnectAsync(ctk);

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
                            TimeSpan.FromSeconds(1),
                        }, (ex, ts) =>
                        {
                            _logger.Warn(ex, "Failed to list folder {0}. Try again soon ...", path);                            
                        });

                    var list = await retrier.ExecuteAsync(async ct1 =>
                    {
                        if (!await conn.IsConnectedAsync(ctk))
                        {
                            conn.Dispose();
                            conn = await GetConnection(ctk);
                            await conn.ConnectAsync(ctk);
                        }
                        return await conn.ListDirectoryAsync(path, ct1);
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
            } finally
            {
                conn?.Dispose();
            }
        }

        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            using (var client = await GetConnection(ctk))
            {
                await client.ConnectAsync(ctk);
                await client.UploadFileAsync(path, content, ctk);
                await client.DisconnectAsync(ctk);
            }
        }

        protected abstract Task<IFtpClientConnection> GetConnection(CancellationToken ctk = default);
    }
}