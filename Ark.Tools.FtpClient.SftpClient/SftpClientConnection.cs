// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using NLog;
using Renci.SshNet;
using Renci.SshNet.Async;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.SftpClient
{
    public class SftpClientConnection : FtpClientConnectionBase  //former KailFtpClient ;-)
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly Renci.SshNet.SftpClient _client;

        private readonly TimeSpan _keepAliveInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _operationTimeout  = TimeSpan.FromMinutes(5);

        [Obsolete("Use the constructor with URI", false)]
        public SftpClientConnection(string host, NetworkCredential credentials, int port = 2222)
            : base(host, credentials)
        {
            Port = port;
            _client = _getSFtpClient();
        }

        //ONLY 1 of this 2
        public SftpClientConnection(Uri uri, NetworkCredential credentials, PrivateKeyFile certificate = null)
            : base(uri, credentials)
        {
            if(certificate == null)
                _client = _getSFtpClient();
            else
                _client = _getSFtpClientWithKeyCertificate(certificate); 
        }

        public SftpClientConnection(Uri uri, NetworkCredential credentials, string certificateFile = null, string certificatePassPhrase = null)
            : base(uri, credentials)
        {
            if (certificateFile == null)
                _client = _getSFtpClient();
            else
            {
                if (string.IsNullOrWhiteSpace(certificatePassPhrase))
                    throw new ArgumentException(message: "CertificatePassPhrase cannot be null or empty.", certificatePassPhrase);

                _client = _getSFtpClientWithKeyCertificate(new PrivateKeyFile(certificateFile, certificatePassPhrase));
            }
        }

        public int Port { get; }
        
        /// <summary>
        /// List all entries of a folder.
        /// </summary>
        /// <param name="path">The folder path to list</param>
        /// <param name="ctk"></param>
        /// <returns>
        /// All entries found (files, folders, symlinks)
        /// </returns>
        public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default(CancellationToken))
        {
            await _ensureConnected(ctk);

            var rawLs = await _client.ListDirectoryAsync(path);
            _logger.Trace("Starting parsing response for path {0}", path);
            return _parse(rawLs);

        }

        private async Task _ensureConnected(CancellationToken ctk)
        {
            if (!_client.IsConnected)
                await Task.Run(() => _client.Connect(), ctk);
        }

        /// <summary>
        /// Downloads the file asynchronous.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="ctk">The CTK.</param>
        /// <returns></returns>
        public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
        {
            await _ensureConnected(ctk);
            using (var ms = new MemoryStream(80 * 1024))
            {
                await _client.DownloadAsync(path, ms, u => { });
                return ms.ToArray();
            }
        }

        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            await _ensureConnected(ctk);
            using (var ms = new MemoryStream(content))
            {
                await _client.UploadAsync(ms, path);
            }
        }

        #region private helpers

        private ConnectionInfo _getConnectionInfo()
        {
            var connrectionInfo = new ConnectionInfo(Uri == null ? Host : Uri.Host, Uri == null ? Port : Uri.Port, Credentials.UserName, new PasswordAuthenticationMethod(Credentials.UserName, Credentials.Password))
            {
                Timeout = TimeSpan.FromMinutes(5),
                RetryAttempts = 2
            };

            return connrectionInfo;
        }

        private Renci.SshNet.SftpClient _getSFtpClient()
        {
            var connInfo = _getConnectionInfo();

            return new Renci.SshNet.SftpClient(connInfo)
            {
                KeepAliveInterval = _keepAliveInterval,
                OperationTimeout = _operationTimeout,
            };
        }

        private Renci.SshNet.SftpClient _getSFtpClientWithKeyCertificate(PrivateKeyFile privateKey)
        {
            var connInfo = _getConnectionInfo();

            return new Renci.SshNet.SftpClient(connInfo.Host, connInfo.Username, new[] { privateKey })
            {
                KeepAliveInterval = _keepAliveInterval,
                OperationTimeout = _operationTimeout,
            };
        }

        private List<FtpEntry> _parse(IEnumerable<SftpFile> files)
        {
            var result = new List<FtpEntry>();

            foreach (var file in files)
            {
                var entry = new FtpEntry();
                entry.FullPath = file.FullName;
                entry.IsDirectory = file.IsDirectory;
                entry.Modified = file.LastWriteTimeUtc;
                entry.Name = file.Name;
                entry.Size = file.Length;
                result.Add(entry);
            }
            return result;
        }

        public override async ValueTask ConnectAsync(CancellationToken ctk)
        {
            if (_client.IsConnected)
                return;

            await Task.Run(() => _client.Connect(), ctk);
        }

        public override ValueTask<bool> IsConnectedAsync(CancellationToken ctk = default)
        {
            if (_client.IsConnected)
                return new ValueTask<bool>(true);

            return new ValueTask<bool>(false);
        }

        public override async ValueTask DisconnectAsync(CancellationToken ctk = default)
        {
            if (!_client.IsConnected)
                return;

            await Task.Run(() => _client.Disconnect(), ctk);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client.Dispose();
            }
        }

        #endregion private helpers

    }

}
