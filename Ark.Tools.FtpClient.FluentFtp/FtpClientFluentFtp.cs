// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using System.Linq;
using Polly;
using Ark.Tools.Core;
using Sunlighter.AsyncQueueLib;
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.FluentFtp
{
    public class FtpClientFluentFtp : Core.IFtpClient
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public FtpClientFluentFtp(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);

            this.Host = host;
            this.Credentials = credentials;
        }

        public NetworkCredential Credentials { get; protected set; }

        public string Host { get; protected set; }

        public async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
        {
            using (var client = _getClient())
            {
                await client.ConnectAsync();
                return await client.DownloadAsync(path);
            }
        }


        public async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default)
        {
            using (var client = _getClient())
            {
                await client.ConnectAsync();
                var lst = await client.GetListingAsync(path, FtpListOption.Auto);
                return lst.Select(x => new FtpEntry()
                {
                    FullPath = x.FullName,
                    IsDirectory = x.Type == FtpFileSystemObjectType.Directory,
                    Modified = x.Modified,
                    Name = x.Name,
                    Size = x.Size
                }).ToList();
            }
        }

        public async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = null, Predicate<FtpEntry> skipFolder = null, CancellationToken ctk = default)
        {
            startPath = startPath ?? "./";

            if (skipFolder == null)
                skipFolder = x => false;

            List<Task<IEnumerable<FtpEntry>>> pending = new List<Task<IEnumerable<FtpEntry>>>();
            IEnumerable<FtpEntry> files = new List<FtpEntry>();

            using (var d = new DisposableContainer())
            {
                var clientsQueue = new AsyncQueue<FluentFTP.IFtpClient>(5);
                for (int i = 0; i<5; i++)
                {
                    var c = _getClient();
                    d.Add(c);
                    await clientsQueue.Enqueue(c, ctk);
                }

                Func<string, CancellationToken, Task<IEnumerable<FtpEntry>>> listFolderAsync = async (string path, CancellationToken ct) =>
                {
                    var c = await clientsQueue.Dequeue(ct);
                    try
                    {
                        var retrier = Policy
                            .Handle<Exception>()
                            .WaitAndRetryAsync(new[]
                            {
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(15)
                            }, (ex, ts) =>
                            {
                                _logger.Warn(ex, "Failed to list folder {0}. Try again soon ...", path);
                            });
                        var res = await retrier.ExecuteAsync(ct1 =>
                        {
                            return c.Value.GetListingAsync(path);
                        }, ct).ConfigureAwait(false);

                        return res.Select(x => new FtpEntry()
                        {
                            FullPath = x.FullName,
                            IsDirectory = x.Type == FtpFileSystemObjectType.Directory,
                            Modified = x.Modified,
                            Name = x.Name,
                            Size = x.Size
                        }).ToList();

                    } finally
                    {
                        await clientsQueue.Enqueue(c.Value, ct);
                    }
                };

                pending.Add(listFolderAsync(startPath, ctk));

                while (pending.Count > 0 && !ctk.IsCancellationRequested)
                {
                    var completedTask = await Task.WhenAny(pending).ConfigureAwait(false);
                    pending.Remove(completedTask);

                    // task could have completed with errors ... strange, let them progate.
                    var list = await completedTask.ConfigureAwait(false);

                    //we want to exclude folders . and .. that we dont want to search
                    foreach (var dir in list.Where(x => x.IsDirectory && !x.Name.Equals(".") && !x.Name.Equals("..")))
                    {
                        if (skipFolder.Invoke(dir))
                            _logger.Debug("Skipping folder: {0}", dir.FullPath);
                        else
                            pending.Add(listFolderAsync(dir.FullPath, ctk));
                    }

                    files = files.Concat(list.Where(x => !x.IsDirectory));
                }

                ctk.ThrowIfCancellationRequested();
                
                return files.ToList();
            }
        }

        public async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            using (var client = _getClient())
            {
                await client.ConnectAsync(ctk);
                await client.UploadAsync(content, path, token:ctk);
                await client.DisconnectAsync(ctk);
            }
        }


        private FluentFTP.IFtpClient _getClient()
        {
            var client = new FluentFTP.FtpClient(Host)
            {
                Credentials = Credentials,
                SocketKeepAlive = true,
                //SocketPollInterval = 1000,
                //ConnectTimeout = 5000,
                //DataConnectionConnectTimeout = 5000,
            };

            return client;
        }
    }
}
