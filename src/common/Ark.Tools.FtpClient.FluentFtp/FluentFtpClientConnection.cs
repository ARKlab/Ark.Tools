// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using FluentFTP;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FtpConfig = Ark.Tools.FtpClient.Core.FtpConfig;

namespace Ark.Tools.FtpClient.FluentFtp
{
    public sealed class FluentFtpClientConnection : FtpClientConnectionBase
    {
        private readonly FluentFTP.IAsyncFtpClient _client;

        private bool _isDisposed;

        public FluentFtpClientConnection(FtpConfig ftpConfig)
            : base(ftpConfig)
        {
            _client = _getClient();
        }

        public override async Task DeleteDirectoryAsync(string path, CancellationToken ctk = default)
        {
            await _client.DeleteDirectory(path, ctk).ConfigureAwait(false);
        }

        public override async Task DeleteFileAsync(string path, CancellationToken ctk = default)
        {
            await _client.DeleteFile(path, ctk).ConfigureAwait(false);
        }

        public override async ValueTask ConnectAsync(CancellationToken ctk)
        {
            if (_client.IsConnected)
                return;

            await _client.Connect(ctk).ConfigureAwait(false);
        }

        public override async ValueTask DisconnectAsync(CancellationToken ctk = default)
        {
            if (!_client.IsConnected)
                return;

            await _client.Disconnect(ctk).ConfigureAwait(false);
        }

        public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
        {
            var res = await _client.DownloadBytes(path, token: ctk).ConfigureAwait(false);
            return res;
        }

        public override ValueTask<bool> IsConnectedAsync(CancellationToken ctk = default)
        {
            return new ValueTask<bool>(_client.IsConnected);
        }

        public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default)
        {
            path ??= "./";
            var lst = await _client.GetListing(path, FtpListOption.Auto, ctk).ConfigureAwait(false);
            var res = lst.Select(x => new FtpEntry()
            {
                FullPath = x.FullName,
                IsDirectory = x.Type == FtpObjectType.Directory,
                Modified = x.Modified,
                Name = x.Name,
                Size = x.Size
            }).ToList();

            return res;
        }

        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            await _client.UploadBytes(content, path, token: ctk).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
                (_client as IDisposable)?.Dispose();
            _isDisposed = true;
        }

        private FluentFTP.IAsyncFtpClient _getClient()
        {
            FluentFTP.AsyncFtpClient client;

            client = new FluentFTP.AsyncFtpClient(Uri.Host, Credentials, Uri.Port, new FluentFTP.FtpConfig
            {
                SocketKeepAlive = true,
            });

            return client;
        }
    }
}