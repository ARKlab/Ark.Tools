// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.SystemNetFtpClient
{
    using Ark.Tools.FtpClient.Core;
    using System.IO;
    using System.Net.FtpClient;
    using System.Net.FtpClient.Async;

    public class SystemNetFtpClientConnection : FtpClientConnectionBase
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly System.Net.FtpClient.IFtpClient _client;

        public SystemNetFtpClientConnection(string host, NetworkCredential credentials, int port = 0)
            : base(host, credentials, port)
        {
            _client = _getClient();
        }

        public override async ValueTask ConnectAsync(CancellationToken ctk)
        {
            if (_client.IsConnected)
                return;

            await _client.ConnectAsync();
        }

        public override async ValueTask DisconnectAsync(CancellationToken ctk = default)
        {
            if (!_client.IsConnected)
                return;

            await _client.DisconnectAsync();
        }

        public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
        {
            await _client.ConnectAsync().ConfigureAwait(false);
            using (var istrm = await _client.OpenReadAsync(path).ConfigureAwait(false))
            using (var ms = new MemoryStream(81920))
            {
                ctk.ThrowIfCancellationRequested();
                await istrm.CopyToAsync(ms, 81920, ctk);
                return ms.ToArray();
            }
        }

        public override ValueTask<bool> IsConnectedAsync(CancellationToken ctk = default)
        {
            return new ValueTask<bool>(_client.IsConnected);
        }

        public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default(CancellationToken))
        {
            await _client.ConnectAsync().ConfigureAwait(false);
            var res = await _client.GetListingAsync(path, options: FtpListOption.Modify | FtpListOption.DerefLinks).ConfigureAwait(false);
            return res.Select(x => new FtpEntry()
            {
                FullPath = x.FullName,
                IsDirectory = x.Type == FtpFileSystemObjectType.Directory,
                Modified = x.Modified,
                Name = x.Name,
                Size = x.Size,
            }).ToList();
        }

        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            await _client.ConnectAsync().ConfigureAwait(false);
            using (var ostrm = await _client.OpenWriteAsync(path).ConfigureAwait(false))
            {
                await ostrm.WriteAsync(content, 0, content.Length, ctk);
                await ostrm.FlushAsync(ctk);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _client?.Dispose();
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
                Port = Port
            };
        }
    }
}
