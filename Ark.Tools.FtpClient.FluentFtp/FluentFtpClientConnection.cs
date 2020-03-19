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
using System.Reactive.Subjects;
using System.Reactive;
using System.Reactive.Linq;

namespace Ark.Tools.FtpClient.FluentFtp
{
    public class FluentFtpClientConnection : FtpClientConnectionBase
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly FluentFTP.IFtpClient _client;

        public FluentFtpClientConnection(string host, NetworkCredential credential) 
            : base(host, credential)
        {
            _client = _getClient();
        }

        public override async ValueTask ConnectAsync(CancellationToken ctk)
        {
            if (_client.IsConnected)
                return;

            await _client.ConnectAsync(ctk);
        }

        public override async ValueTask DisconnectAsync(CancellationToken ctk = default)
        {
            if (!_client.IsConnected)
                return;

            await _client.DisconnectAsync(ctk);
        }

        public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
        {
            var res = await _client.DownloadAsync(path);
            return res;
        }

        public override ValueTask<bool> IsConnectedAsync(CancellationToken ctk = default)
        {
            return new ValueTask<bool>(_client.IsConnected);
        }

        public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default)
        {
            var lst = await _client.GetListingAsync(path, FtpListOption.Auto);
            var res = lst.Select(x => new FtpEntry()
            {
                FullPath = x.FullName,
                IsDirectory = x.Type == FtpFileSystemObjectType.Directory,
                Modified = x.Modified,
                Name = x.Name,
                Size = x.Size
            }).ToList();

            return res;
        }

        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            await _client.UploadAsync(content, path, token:ctk);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _client?.Dispose();
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
