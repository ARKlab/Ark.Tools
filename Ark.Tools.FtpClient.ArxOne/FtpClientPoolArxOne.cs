﻿// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using Polly;
using System.IO;
using ArxOne.Ftp;
using EnsureThat;
using Ark.Tools.FtpClient.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using Org.Mentalis.Network.ProxySocket;

namespace Ark.Tools.FtpClient
{

    public class FtpClientPoolArxOne : FtpClientBase, IFtpClientPool
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly ArxOne.Ftp.FtpClient _client;
        private readonly SemaphoreSlim _semaphore;

        public FtpClientPoolArxOne(int maxPoolSize, string host, NetworkCredential credentials)
            : base(host, credentials, maxPoolSize)
        {
            _client = _getClient();
            _semaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);
        }

        private protected virtual ArxOne.Ftp.FtpClient _getClient()
        {
            return new ArxOne.Ftp.FtpClient(
                new Uri("ftp://" + this.Host), this.Credentials, new FtpClientParameters()
                {
                    ConnectTimeout = TimeSpan.FromSeconds(60),
                    ReadWriteTimeout = TimeSpan.FromMinutes(3),
                    Passive = true,
                });
        }

        public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
        {
            await _semaphore.WaitAsync(ctk);
            try
            {
                using (var istrm = _client.Retr(path, FtpTransferMode.Binary))
                using (var ms = new MemoryStream(81920))
                {
                    await istrm.CopyToAsync(ms, 81920, ctk);
                    return ms.ToArray();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            await _semaphore.WaitAsync(ctk);
            try
            {
                using (var ostrm = _client.Stor(path, FtpTransferMode.Binary))
                {
                    await ostrm.WriteAsync(content, 0, content.Length, ctk);
                    await ostrm.FlushAsync(ctk);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private IEnumerable<ArxOne.Ftp.FtpEntry> _list(string path)
        {
            if (_client.ServerFeatures.HasFeature("MLSD"))
            {
                return _client.MlsdEntries(path);
            }
            else
            {
                return _client.ListEntries(path);
            }
        }

        public override async Task<IEnumerable<Core.FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default(CancellationToken))
        {
            await _semaphore.WaitAsync(ctk);
            try
            {                
                path = path ?? "./";

                var list = _list(path);
                return list.Select(x => new Core.FtpEntry
                {
                    FullPath = x.Path.ToString(),
                    IsDirectory = x.Type == FtpEntryType.Directory,
                    Modified = x.Date,
                    Name = x.Name,
                    Size = x.Size.GetValueOrDefault(-1),
                }).ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client?.Dispose();
                _semaphore?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
