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

    public class SystemNetFtpClient : FtpClientBase
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public SystemNetFtpClient(string host, NetworkCredential credentials)
            : base(host, credentials)
        {
        }

        public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
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

        public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default(CancellationToken))
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


        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
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
