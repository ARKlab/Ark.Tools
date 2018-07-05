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
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.FtpProxy
{
    public class FtpClientProxyFactory : IFtpClientFactory
    {
        private readonly IFtpClientProxyConfig _config;
        private static readonly IFlurlClientFactory _flurlClientFactory = new PerHostFlurlClientFactory();

        static FtpClientProxyFactory()
        {
            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, sslPolicyErrors) => true;
        }

        public FtpClientProxyFactory(IFtpClientProxyConfig config)
        {
            EnsureArg.IsNotNull(config);

            _config = config;
        }

        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            return new FtpClientProxy(_config, _flurlClientFactory              
                , host, credentials);
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

    public class FtpClientProxy : IFtpClient
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private IFtpClientProxyConfig _config;
        private readonly AuthenticationContext _adal;
        private readonly AuthenticationApiClient _auth0;

        private readonly ConnectionInfo _connectionInfo;

        private readonly IFlurlClient _client;

        private readonly Polly.Caching.Memory.MemoryCacheProvider _memoryCacheProvider
           = new Polly.Caching.Memory.MemoryCacheProvider(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        private readonly Policy<(string AccessToken, DateTimeOffset ExpiresOn)> _cachePolicy;

        public FtpClientProxy(IFtpClientProxyConfig config, IFlurlClientFactory client, string host, NetworkCredential credentials)
        {
            this._config = config;
            this.Host = host;
            this.Credentials = credentials;
            

            var cfg = new JsonSerializerSettings();

            cfg.ObjectCreationHandling = ObjectCreationHandling.Replace;
            cfg.NullValueHandling = NullValueHandling.Ignore;

            _client = client.Get(_config.FtpProxyWebInterfaceBaseUri)
                .Configure(c => 
                {
                    c.ConnectionLeaseTimeout = TimeSpan.FromMinutes(30);
                    c.JsonSerializer = new NewtonsoftJsonSerializer(cfg);                    
                })
                .WithHeader("Accept", "application/json, text/json")
                .WithHeader("Accept-Encoding", "gzip, deflate")
                .WithTimeout(TimeSpan.FromMinutes(20))
                .AllowHttpStatus(HttpStatusCode.NotFound)
                ;

            _client.BaseUrl = _config.FtpProxyWebInterfaceBaseUri.ToString();

            if (_config.UseAuth0) _auth0 = new AuthenticationApiClient(_config.TenantID);
            else _adal = new AuthenticationContext("https://login.microsoftonline.com/" + this._config.TenantID);

            _cachePolicy = Policy.CacheAsync(_memoryCacheProvider.AsyncFor<(string AccessToken, DateTimeOffset ExpiresOn)>(), new ResultTtl<(string AccessToken, DateTimeOffset ExpiresOn)>(r => new Ttl(r.ExpiresOn - DateTimeOffset.Now, false)));
            //_cachePolicy = Policy.CacheAsync(_memoryCacheProvider, new RelativeTtl(TimeSpan.FromMinutes(5)));

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
                    })
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
                        })
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
            var res = await _cachePolicy.ExecuteAsync((ctx,ct) =>
            {
                if (_config.UseAuth0) return _getAuth0AccessToken(ct);
                else return _getAdalAccessToken(ct);
            }, new Context("_getAccessToken"), ctk);

            return res.AccessToken;
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

        //private void _handleRestResult(IRestResponse response)
        //{
        //    if (response.ResponseStatus == ResponseStatus.Completed)
        //    {
        //        if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        //        {
        //            // nothing
        //        }
        //        else
        //        {
        //            _logger.Trace("Failed handling REST call to WebInterface: {0}", response.Request.Resource);
        //            throw new ApplicationException(string.Format("Failed handling REST call to WebInterface {0}. Returned status: {1}. Content: \n{2}", response.Request.Resource, response.StatusCode, response.Content.Replace("\\r\\n", "\n")));
        //        }
        //    }
        //    else
        //    {
        //        if (response.ErrorException != null)
        //        {
        //            _logger.Trace(response.ErrorException, "Failed handling REST call to WebInterface: {0}", response.Request.Resource);
        //            throw new ApplicationException("Failed handling REST call to WebInterface: " + response.Request.Resource, response.ErrorException);
        //        } else {
        //            _logger.Trace("Failed handling REST call to WebInterface: {0} status: {1}", response.Request.Resource, response.ResponseStatus);
        //            throw new ApplicationException($"Failed handling REST call to WebInterface: {response.Request.Resource} status: {response.ResponseStatus}");
        //        }
        //    }
        //}

        public Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            throw new NotImplementedException();
        }
    }
}
