// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using Ark.Tools.FtpClient;
using NodaTime;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{

    public class FtpWorkerHost<TPayload> : WorkerHost<FtpFile<TPayload>, FtpMetadata, FtpFilter>
    {
        public FtpWorkerHost(IFtpHostConfig config)
            : base(config)
        {
            this.Use(d =>
            {
                d.Container.RegisterInstance(config);
                d.Container.RegisterInstance(config as IFtpConfig);
            });
        }

        /// <summary>
        /// Set the FtpClientFactory to use
        /// </summary>
        /// <typeparam name="TFtpFactory">IFtpClientFactory implementation</typeparam>
        /// <param name="deps">Callback to register additinal dependencies of the TFtpFactory</param>
        public void UseFtpClientFactory<TFtpFactory>(Action<Dependencies> deps = null)
            where TFtpFactory : class, IFtpClientFactory
        {
            base.UseDataProvider<FtpProvider>(r =>
            {
                deps?.Invoke(r);
                r.Container.RegisterSingleton<IFtpClientFactory, TFtpFactory>();
            });
        }

        /// <summary>
        /// Set the parser implementation used to parse the files retrived from the FTP
        /// </summary>
        /// <typeparam name="TParser">The parser implementation</typeparam>
        /// <param name="deps">Callback to register additinal dependencies of the TFtpFactory</param>
        public void UseParser<TParser>(Action<Dependencies> deps = null)
            where TParser : class, IFtpParser<TPayload>
        {
            this.Use(d =>
            {
                deps?.Invoke(d);
                d.Container.RegisterSingleton<IFtpParser<TPayload>, TParser>();
            });
        }

        class FtpProvider : IResourceProvider<FtpMetadata, FtpFile<TPayload>, FtpFilter>
        {
            private readonly IFtpConfig _config;
            private readonly IFtpClient _ftpClient;
            private readonly IFtpParser<TPayload> _parser;

            public FtpProvider(IFtpConfig config, IFtpClientFactory ftpClientFactory, IFtpParser<TPayload> parser)
            {
                _config = config;
                _ftpClient = ftpClientFactory.Create(config.Host, config.Credentials);
                _parser = parser;
            }

            public async Task<IEnumerable<FtpMetadata>> GetMetadata(FtpFilter filter, CancellationToken ctk = default)
            {

                IEnumerable<FtpMetadata> res = new FtpMetadata[0];
                using (var cts1 = new CancellationTokenSource(_config.ListingTimeout))
                using (var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, ctk))
                {
                    foreach (var f in filter.FoldersToWatch)
                    {
                        var list = await _ftpClient.ListFilesRecursiveAsync(f, x => filter.FolderFilter?.Invoke(x.FullPath) == false, ctk: cts2.Token).ConfigureAwait(false);
                        res = res.Concat(list.Select(e => new FtpMetadata(e)));
                    }
                    return res.ToList();
                }
            }

            public async Task<FtpFile<TPayload>> GetResource(FtpMetadata metadata, IResourceTrackedState lastState, CancellationToken ctk = default)
            {
                var contents = await Policy
                    .Handle<Exception>()
                    .RetryAsync(3)
                    .ExecuteAsync(async ct =>
                    {
                        using (var cts1 = new CancellationTokenSource(_config.DownloadTimeout))
                        using (var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, ct))
                        {
                            return await _ftpClient.DownloadFileAsync(metadata.Entry.FullPath, ctk: cts2.Token).ConfigureAwait(false);

                        }
                    }, ctk).ConfigureAwait(false);

                var checksum = _computeChecksum(contents);
                if (lastState?.CheckSum == checksum)
                    return null;

                var file = new FtpFile<TPayload>(metadata)
                {
                    CheckSum = checksum,
                    RetrievedAt = SystemClock.Instance.GetCurrentInstant(),
                    ParsedData = _parser.Parse(metadata, contents)
                };

                return file;
            }

            private string _computeChecksum(byte[] contents)
            {
                using (var hash = MD5.Create())
                {
                    var h = hash.ComputeHash(contents);
                    return h.ToHexString();
                }
            }

        }
    }
}
