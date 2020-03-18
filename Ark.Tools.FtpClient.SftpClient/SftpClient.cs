// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Renci.SshNet.Async;
using EnsureThat;
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.SftpClient
{
    public sealed class SFtpClientFactory : DefaultFtpClientFactory
    {
        public SFtpClientFactory() 
            : base(new SFtpClientConnectionFactory())
        {
        }
    }

    public sealed class SFtpClientPoolFactory : DefaultFtpClientPoolFactory
    {
        public SFtpClientPoolFactory() 
            : base(new SFtpClientConnectionFactory())
        {
        }
    }

    public sealed class SFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public IFtpClientConnection Create(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);

            int port = 2222;
            string h = host;
            var r = Regex.Match(host, @":\d+$");
            if (r.Success)
            {
                h = host.Substring(0, r.Index);
                port = Convert.ToInt16(host.Substring(r.Index + 1));
            }

            return new SftpClientConnection(h, credentials, port);
        }
    }

    internal enum ListStyle
    {
        Unix,
        Windows
    }
    
    public class SftpClientConnection : FtpClientConnectionBase  //former KailFtpClient ;-)
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly Renci.SshNet.SftpClient _client;

        public SftpClientConnection(string host, NetworkCredential credentials, int port = 2222)
            : base(host, credentials)
        {
            Port = port;
            _client = _getSFtpClient();
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

        private Renci.SshNet.SftpClient _getSFtpClient()
        {
            var connInfo = new ConnectionInfo(Host, Port, Credentials.UserName, new PasswordAuthenticationMethod(Credentials.UserName, Credentials.Password));
            connInfo.Timeout = TimeSpan.FromMinutes(5);
            connInfo.RetryAttempts = 2;
           
            return new Renci.SshNet.SftpClient(connInfo)
            {
                KeepAliveInterval = TimeSpan.FromMinutes(1),
                OperationTimeout = TimeSpan.FromMinutes(5),
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



    internal static class FtpPathExtensions
    {

        public static string GetFtpPath(this string path)
        {
            if (String.IsNullOrEmpty(path))
                return "./";

            path = Regex.Replace(path.Replace('\\', '/'), "[/]+", "/").TrimEnd('/');
            if (path.Length == 0)
                path = "./";

            return path;
        }

        public static string GetFtpPath(this string path, params string[] segments)
        {
            if (String.IsNullOrEmpty(path))
                path = "./";

            foreach (string part in segments)
            {
                if (part != null)
                {
                    if (path.Length > 0 && !path.EndsWith("/"))
                        path += "/";
                    path += Regex.Replace(part.Replace('\\', '/'), "[/]+", "/").TrimEnd('/');
                }
            }

            path = Regex.Replace(path.Replace('\\', '/'), "[/]+", "/").TrimEnd('/');
            if (path.Length == 0)
                path = "./";

            /*if (!path.StartsWith("/") || !path.StartsWith("./"))
                path = "./" + path;*/

            return path;
        }

        public static string GetFtpFileName(this string path)
        {
            string tpath = (path == null ? null : path);
            int lastslash = -1;

            if (tpath == null)
                return null;

            lastslash = tpath.LastIndexOf('/');
            if (lastslash < 0)
                return tpath;

            lastslash += 1;
            if (lastslash >= tpath.Length)
                return tpath;

            return tpath.Substring(lastslash, tpath.Length - lastslash);
        }

        public static DateTime GetFtpDate(this string date, DateTimeStyles style)
        {
            string[] formats = new string[] {
                "yyyyMMddHHmmss",
                "yyyyMMddHHmmss.fff",
                "MMM dd  yyyy",
                "MMM  d  yyyy",
                "MMM dd HH:mm",
                "MMM  d HH:mm",
                "MM-dd-yy  hh:mmtt",
                "MM-dd-yyyy  hh:mmtt"
            };
            DateTime parsed;

            if (DateTime.TryParseExact(date, formats, CultureInfo.InvariantCulture, style, out parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }
    }

}
