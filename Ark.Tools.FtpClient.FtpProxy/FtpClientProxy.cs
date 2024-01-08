// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using Ark.Tools.Http;

using Flurl.Http;
using Flurl.Http.Configuration;

using NLog;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.FtpProxy
{

    public sealed class FtpClientProxy : IFtpClientPool
    {
        private readonly IFtpClientProxyConfig _config;
        private readonly TokenProvider _tokenProvider;
        private readonly ConnectionInfo _connectionInfo;

        private readonly IFlurlClient _client;
        private bool _isDisposed = false;

        public FtpClientProxy(IFtpClientProxyConfig config, FtpConfig ftpConfig)
            : this(config, new TokenProvider(config), ftpConfig)
        {
            FtpConfig = ftpConfig;
        }

        internal FtpClientProxy(IFtpClientProxyConfig config, TokenProvider tokenProvider, FtpConfig ftpConfig)
        {
            _config = config;

            this.Uri = ftpConfig.Uri;
            this.Credentials = ftpConfig.Credentials;

            this.FtpConfig = ftpConfig;

            _tokenProvider = tokenProvider;

            _client = _initClient();

            _connectionInfo = _initConnectionInfo();
        }

        public Uri Uri { get; private set; }

        public NetworkCredential? Credentials { get; private set; }
        public FtpConfig FtpConfig { get; private set; }

        class DownloadFileResult
        {
            public byte[]? Content { get; set; }
        }

        class ConnectionInfo
        {
            public Uri? Uri { get; set; }
            public string? Username { get; set; }
            public string? Password { get; set; }
        }

        class ListingRequest
        {
            public ConnectionInfo? Info { get; set; }
            public int? DegreeOfParallelism { get; set; }
            public bool? Recursive { get; set; }
            public string[]? Paths { get; set; }
        }

        /// <summary>
        /// Download a file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <param name="ctk"></param>
        /// <returns>
        /// The byte[] of the contents of the file.
        /// </returns>
        public async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
        {
            var tok = await _getAccessToken(ctk);

            var res = await _client.Request("v2", "DownloadFile")
                .SetQueryParam("filePath", path)
                .WithOAuthBearerToken(tok)
                .PostJsonAsync(_connectionInfo, cancellationToken: ctk)
                .ReceiveJson<DownloadFileResult>()
                ;

            return res.Content ?? Array.Empty<byte>();
        }

        /// <summary>
        /// List all entries of a folder.
        /// </summary>
        /// <param name="path">The folder path to list</param>
        /// <param name="ctk"></param>
        /// <returns>
        /// All entries found (files, folders, symlinks)
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default)
        {
            path ??= "./";
            var tok = await _getAccessToken(ctk);
            
            var res = await _client.Request("v2", "ListFolder")
                .WithOAuthBearerToken(tok)
                .PostJsonAsync(new ListingRequest
                {
                    Info = _connectionInfo,
                    Paths = new[] { path },
                    Recursive = false,
                }, cancellationToken: ctk)
                .ReceiveJson<IEnumerable<FtpEntry>>()
                ;

            return res;
        }

        /// <summary>
        /// List a directory recursively and returns the files found.
        /// </summary>
        /// <param name="startPath">The directory to list recursively</param>
        /// <param name="skipFolder">Predicate returns true for folders that are to be skipped.</param>
        /// <param name="ctk"></param>
        /// <returns>
        /// The files found.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default)
        {
            startPath ??= "./";
            var tok = await _getAccessToken(ctk);
            if (skipFolder == null) // no folders to skip, just recurse overall
            {
                var res = await _client.Request("v2", "ListFolder")
                    .WithOAuthBearerToken(tok)
                    .PostJsonAsync(new ListingRequest
                    {
                        Info = _connectionInfo,
                        Paths = new[] { startPath },
                        Recursive = true,
                        DegreeOfParallelism = _config.ListingDegreeOfParallelism
                    }, cancellationToken: ctk)
                    .ReceiveJson<IEnumerable<FtpEntry>>()
                    ;
                
                return res.Where(e => !e.IsDirectory);
            }
            else
            {
                var entries = new List<IEnumerable<FtpEntry>>();

                var res = await _client.Request("v2", "ListFolder")
                    .WithOAuthBearerToken(tok)
                    .PostJsonAsync(new ListingRequest
                    {
                        Info = _connectionInfo,
                        Paths = new[] { startPath },
                        Recursive = false,
                    }, cancellationToken: ctk)
                    .ReceiveJson<IEnumerable<FtpEntry>>()
                    ;

                entries.Add(res);

                var folders = res.Where(x => x.IsDirectory && !skipFolder(x)).ToArray();
                while (folders.Length > 0)
                {
                    var r = await _client.Request("v2", "ListFolder")
                        .WithOAuthBearerToken(tok)
                        .PostJsonAsync(new ListingRequest
                        {
                            Info = _connectionInfo,
                            Paths = folders.Select(f => f.FullPath).ToArray(),
                            Recursive = false,
                        }, cancellationToken: ctk)
                        .ReceiveJson<IEnumerable<FtpEntry>>()
                        ;

                    entries.Add(r);
                    folders = r.Where(x => x.IsDirectory && !skipFolder(x)).ToArray();
                }

                return entries.SelectMany(x => x.Where(e => !e.IsDirectory));
            }            
        }

        public Task DeleteFileAsync(string path, CancellationToken ctk = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteDirectoryAsync(string path, CancellationToken ctk = default)
        {
            throw new NotImplementedException();
        }

        private Task<string> _getAccessToken(CancellationToken ctk = default)
        {
            return _tokenProvider.GetToken(ctk);
        }

        private IFlurlClient _initClient()
        {
            var flurlClient = new FlurlClientBuilder(_config.FtpProxyWebInterfaceBaseUri.ToString())
                .ConfigureArkDefaults()
                .ConfigureInnerHandler(h => {
                    h.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;
                })
                .WithHeader("Accept", "application/json, text/json")
                .WithHeader("Accept-Encoding", "gzip, deflate")
                .WithTimeout(TimeSpan.FromMinutes(20))
                .Build()
                ;

            return flurlClient;
        }
        private ConnectionInfo _initConnectionInfo()
        {
            return new ConnectionInfo
            {
                Uri = this.Uri,
                Username = this.Credentials?.UserName,
                Password = this.Credentials?.Password,
            };
        }


        public Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _client?.Dispose();

            _isDisposed = true;
        }
    }
}
