// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Auth0;
using Ark.Tools.FtpClient.Core;
using Ark.Tools.Http;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using EnsureThat;
using Flurl.Http;
using Flurl.Http.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using NLog;
using Polly;
using Polly.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.FtpProxy
{
    public class FtpClientProxyFactory : IFtpClientFactory
    {
        private readonly IFtpClientProxyConfig _config;

        public FtpClientProxyFactory(IFtpClientProxyConfig config)
        {
            EnsureArg.IsNotNull(config);
            _config = config;
        }

        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            return new FtpClientProxy(_config, ArkFlurlClientFactory.Instance, host, credentials);
        }
    }

    public class FtpClientPoolProxyFactory : IFtpClientPoolFactory
    {
        private readonly IFtpClientProxyConfig _config;

        public FtpClientPoolProxyFactory(IFtpClientProxyConfig config)
        {
            EnsureArg.IsNotNull(config);
            _config = config;
        }

        public IFtpClientPool Create(int maxPoolSize, string host, NetworkCredential credentials)
        {
            return new FtpClientProxy(_config, ArkFlurlClientFactory.Instance, host, credentials);
        }
    }
    
    public interface IFtpClientProxyConfig
    {
        string ClientID { get; }
        string ClientKey { get;  }
        Uri FtpProxyWebInterfaceBaseUri { get;  }
        string ApiIdentifier { get; }
        string TenantID { get; }
        bool UseAuth0 { get; }
        int? ListingDegreeOfParallelism { get; }
    }

    public sealed class FtpClientProxy : IFtpClientPool
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private IFtpClientProxyConfig _config;
        private readonly AuthenticationContext _adal;
        private readonly IAuthenticationApiClient _auth0;

        private readonly ConnectionInfo _connectionInfo;

        private readonly IFlurlClient _client;

        public FtpClientProxy(IFtpClientProxyConfig config, IFlurlClientFactory client, string host, NetworkCredential credentials)
        {
            this._config = config;
            this.Host = host;
            this.Credentials = credentials;

            _client = client.Get(_config.FtpProxyWebInterfaceBaseUri)
                .Configure(c => 
                {
                    c.HttpClientFactory = new UntrustedCertClientFactory();
                    c.ConnectionLeaseTimeout = TimeSpan.FromMinutes(30);                  
                })
                .WithHeader("Accept", "application/json, text/json")
                .WithHeader("Accept-Encoding", "gzip, deflate")
                .WithTimeout(TimeSpan.FromMinutes(20))
                .AllowHttpStatus(HttpStatusCode.NotFound)
                ;

            _client.BaseUrl = _config.FtpProxyWebInterfaceBaseUri.ToString();

            if (_config.UseAuth0) 
                _auth0 = new AuthenticationApiClientCachingDecorator(new AuthenticationApiClient(_config.TenantID));
            else 
                _adal = new AuthenticationContext("https://login.microsoftonline.com/" + this._config.TenantID);

            _connectionInfo = new ConnectionInfo
            {
                Host = this.Host,
                Username = this.Credentials.UserName,
                Password = this.Credentials.Password,
            };
        }

        public string Host { get; private set; }

        public NetworkCredential Credentials { get; private set; }

        class DownloadFileResult
        {
            public byte[] Content { get; set; }
        }

        class ConnectionInfo
        {
            public string Host { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }

        class ListingRequest
        {
            public ConnectionInfo Info { get; set; }
            public int? DegreeOfParallelism { get; set; }
            public bool? Recursive { get; set; }
            public string[] Paths { get; set; }
        }

        /// <summary>
        /// Download a file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <param name="ctk"></param>
        /// <returns>
        /// The byte[] of the contents of the file.
        /// </returns>
        public async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
        {
            var tok = await _getAccessToken(ctk).ConfigureAwait(false);

            var res = await _client.Request("v2", "DownloadFile")
                .SetQueryParam("filePath", path)
                .WithOAuthBearerToken(tok)
                .PostJsonAsync(_connectionInfo, ctk)
                .ReceiveJson<DownloadFileResult>()
                ;

            return res.Content;
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
        public async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default(CancellationToken))
        {
            var tok = await _getAccessToken(ctk).ConfigureAwait(false);
            
            var res = await _client.Request("v2", "ListFolder")
                .WithOAuthBearerToken(tok)
                .PostJsonAsync(new ListingRequest
                {
                    Info = _connectionInfo,
                    Paths = path != null ? new[] { path } : null,
                    Recursive = false,
                }, ctk)
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
        public async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = null, Predicate<FtpEntry> skipFolder = null, CancellationToken ctk = default(CancellationToken))
        {
            var tok = await _getAccessToken(ctk).ConfigureAwait(false);
            if (skipFolder == null) // no folders to skip, just recurse overall
            {
                var res = await _client.Request("v2", "ListFolder")
                    .WithOAuthBearerToken(tok)
                    .PostJsonAsync(new ListingRequest
                    {
                        Info = _connectionInfo,
                        Paths = startPath != null ? new[] { startPath } : null,
                        Recursive = true,
                        DegreeOfParallelism = _config.ListingDegreeOfParallelism
                    }, ctk)
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
                        Paths = startPath != null ? new[] { startPath } : null,
                        Recursive = false,
                    }, ctk)
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
                        }, ctk)
                        .ReceiveJson<IEnumerable<FtpEntry>>()
                        ;

                    entries.Add(r);
                    folders = r.Where(x => x.IsDirectory && !skipFolder(x)).ToArray();
                }

                return entries.SelectMany(x => x.Where(e => !e.IsDirectory));
            }            
        }

        private async Task<string> _getAccessToken(CancellationToken ctk = default(CancellationToken))
        {
            if (_config.UseAuth0) 
                return (await _getAuth0AccessToken(ctk)).AccessToken;
            else 
                return (await _getAdalAccessToken(ctk)).AccessToken;
        }

        private async Task<(string AccessToken, System.DateTimeOffset ExpiresOn)> _getAuth0AccessToken(CancellationToken ctk = default(CancellationToken))
        {

            try
            {
                var result = await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(3))
                    .ExecuteAsync(() => _auth0.GetTokenAsync(new ClientCredentialsTokenRequest
                    {
                        Audience = _config.ApiIdentifier,
                        ClientId = _config.ClientID,
                        ClientSecret = _config.ClientKey
                    }))
                    .ConfigureAwait(false);

                return (result.AccessToken, DateTimeOffset.Now.AddSeconds(result.ExpiresIn));
            }
            catch (Exception ex)
            {
                throw new AuthenticationException("Failed to acquire token, check credentials", ex);
            }
        }

        private async Task<(string AccessToken, System.DateTimeOffset ExpiresOn)> _getAdalAccessToken(CancellationToken ctk = default(CancellationToken))
        {
            AuthenticationResult result = null;
            try
            {
                result = await Policy
                    .Handle<AdalException>(ex => ex.ErrorCode == "temporarily_unavailable")
                    .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(3))
                    .ExecuteAsync(c => _adal.AcquireTokenAsync(_config.ApiIdentifier, new ClientCredential(this._config.ClientID, this._config.ClientKey)), ctk, false)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new AuthenticationException("Failed to acquire token, check credentials", ex);
            }

            if (result == null)
                throw new AuthenticationException("Failed to acquire token, check credentials");

            return (result.AccessToken, result.ExpiresOn);
        }
        
        public Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

    }
}
