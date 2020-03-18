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
    public class FtpClientFluentFtp : FtpClientBase
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        
        public FtpClientFluentFtp(string host, NetworkCredential credential) : base(host, credential)
        {
        }

        public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
        {
            using (var client = _getClient())
            {
                await client.ConnectAsync();
                var res = await client.DownloadAsync(path);
                await client.DisconnectAsync(ctk);
                return res;
            }
        }


        public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default)
        {
            using (var client = _getClient())
            {
                await client.ConnectAsync();
                var lst = await client.GetListingAsync(path, FtpListOption.Auto);
                var res = lst.Select(x => new FtpEntry()
                {
                    FullPath = x.FullName,
                    IsDirectory = x.Type == FtpFileSystemObjectType.Directory,
                    Modified = x.Modified,
                    Name = x.Name,
                    Size = x.Size
                }).ToList();
                await client.DisconnectAsync(ctk);

                return res;
            }
        }

        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
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
