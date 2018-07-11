// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace Ark.Tools.FtpClient.SystemNetFtpClient
{
    using Ark.Tools.FtpClient.Core;
    using EnsureThat;
    using Polly;
    using System.IO;
    using System.Net.FtpClient;
    using System.Net.FtpClient.Async;

    public class SystemNetFtpClientFactory : IFtpClientFactory
    {
        public Ark.Tools.FtpClient.Core.IFtpClient Create(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new SystemNetFtpClient(host, credentials);
        }
    }

    public class SystemNetFtpClient : Ark.Tools.FtpClient.Core.IFtpClient
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public SystemNetFtpClient(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);

            this.Host = host;
            this.Credentials = credentials;
        }

        public NetworkCredential Credentials { get; protected set; }

        public string Host { get; protected set; }

        public async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
        {
            using (var client = _getClient())
            {
                await client.ConnectAsync().ConfigureAwait(false);
                using (var istrm = await client.OpenReadAsync(path).ConfigureAwait(false))
                using (var ms = new MemoryStream())
                {
                    ctk.ThrowIfCancellationRequested();
                    await istrm.CopyToAsync(ms, 81920, ctk);
                    return ms.ToArray();
                }
            }
        }

        public async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default(CancellationToken))
        {
            using (var client = _getClient())
            {
                await client.ConnectAsync().ConfigureAwait(false);
                var res = await client.GetListingAsync(path, options: FtpListOption.Modify | FtpListOption.DerefLinks).ConfigureAwait(false);
                return res.Select(x => new FtpEntry()
                {
                    FullPath = x.FullName,
                    IsDirectory = x.Type == FtpFileSystemObjectType.Directory,
                    Modified = x.Modified,
                    Name = x.Name,
                    Size = x.Size,
                });
            }
        }

        public async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = null, Predicate<FtpEntry> skipFolder = null, CancellationToken ctk = default(CancellationToken))
        {
            if (skipFolder == null)
                skipFolder = x => false;


            List<Task<IEnumerable<FtpEntry>>> pending = new List<Task<IEnumerable<FtpEntry>>>();
            IEnumerable<FtpEntry> files = new List<FtpEntry>();

            using (var client = _getClient())
            {
                await client.ConnectAsync().ConfigureAwait(false);

                Func<string, CancellationToken, Task<IEnumerable<FtpEntry>>> listFolderAsync = async (string path, CancellationToken ct) =>
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
                    var res = await retrier.ExecuteAsync(async ct1 =>
                    {
                        return await client.GetListingAsync(path, options: FtpListOption.Modify | FtpListOption.DerefLinks).ConfigureAwait(false);
                    }, ct).ConfigureAwait(false);
                    return res.Select(x => new FtpEntry()
                    {
                        FullPath = x.FullName,
                        IsDirectory = x.Type == FtpFileSystemObjectType.Directory,
                        Modified = x.Modified,
                        Name = x.Name,
                        Size = x.Size,
                    });
                };

                pending.Add(listFolderAsync(startPath, ctk));

                while (pending.Count > 0 && !ctk.IsCancellationRequested)
                {
                    var completedTask = await Task.WhenAny(pending).ConfigureAwait(false);
                    pending.Remove(completedTask);

                    // task could have completed with errors ... strange, let them progate.
                    var list = await completedTask.ConfigureAwait(false);

                    //we want to exclude folders . and .. that we dont want to search
                    foreach (var d in list.Where(x => x.IsDirectory && !x.Name.Equals(".") && !x.Name.Equals("..")))
                    {
                        if (skipFolder.Invoke(d))
                            _logger.Info("Skipping folder: {0}", d.FullPath);
                        else
                            pending.Add(listFolderAsync(d.FullPath, ctk));
                    }

                    files = files.Concat(list.Where(x => !x.IsDirectory).ToList());
                }

                ctk.ThrowIfCancellationRequested();

                return files;
            }
        }

        public async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            using (var client = _getClient())
            {
                await client.ConnectAsync().ConfigureAwait(false);
                using (var ostrm = await client.OpenWriteAsync(path).ConfigureAwait(false))
                {
                    await ostrm.WriteAsync(content, 0, content.Length, ctk);
                    await ostrm.FlushAsync(ctk);
                }
            }
        }

        private System.Net.FtpClient.IFtpClient _getClient()
        {
            return new System.Net.FtpClient.FtpClient()
            {
                Credentials = this.Credentials,
                ConnectTimeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds,
                DataConnectionConnectTimeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds,
                DataConnectionReadTimeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds, // listing takes time
                ReadTimeout = (int)TimeSpan.FromSeconds(15).TotalMilliseconds,
                Host = this.Host,
                InternetProtocolVersions = FtpIpVersion.IPv4,                
                SocketKeepAlive = true,
                StaleDataCheck = false,
                UngracefullDisconnection = false,
            };
        }
    }
}
