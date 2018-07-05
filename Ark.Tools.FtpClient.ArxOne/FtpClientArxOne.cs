using NLog;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace Ark.Tools.FtpClient
{
    using Polly;
    using System.IO;
    using ArxOne.Ftp;
    using EnsureThat;

    public class FtpClientArxOneFactory : IFtpClientFactory
    {
        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new FtpClientArxOne(host, credentials);
        }
    }

    public class FtpClientArxOne : IFtpClient
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public FtpClientArxOne(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);

            this.Host = host;
            this.Credentials = credentials;
        }

        public NetworkCredential Credentials { get; protected set; }

        public string Host { get; protected set; }

        public async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
        {
            using (var client = _getClient())
            {
                using (var istrm = client.Retr(path, FtpTransferMode.Binary))
                using (var ms = new MemoryStream())
                {
                    await istrm.CopyToAsync(ms, 81920, ctk);
                    return ms.ToArray();
                }
            }
        }

        public async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            using (var client = _getClient())
            {
                using (var ostrm = client.Stor(path, FtpTransferMode.Binary))
                {
                    await ostrm.WriteAsync(content, 0, content.Length, ctk);
                    await ostrm.FlushAsync(ctk);
                }
            }
        }

        private IEnumerable<ArxOne.Ftp.FtpEntry> _list(ArxOne.Ftp.FtpClient client, string path)
        {
            if (client.ServerFeatures.HasFeature("MLSD"))
            {
                return client.MlsdEntries(path);
            } else
            {
                return client.ListEntries(path);
            }
        }

        public Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default(CancellationToken))
        {
            path = path ?? "./";
            return Task.Run<IEnumerable<FtpEntry>>(() =>
            {
                using (var client = _getClient())
                {
                    var list = _list(client, path);
                    return list.Select(x => new FtpEntry
                    {
                        FullPath = x.Path.ToString(),
                        IsDirectory = x.Type == FtpEntryType.Directory,
                        Modified = x.Date,
                        Name = x.Name,
                        Size = x.Size.GetValueOrDefault(-1),
                    }).ToArray();
                }
            }, ctk);
        }

        public async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = null, Predicate<FtpEntry> skipFolder = null, CancellationToken ctk = default(CancellationToken))
        {
            startPath = startPath ?? "./";

            if (skipFolder == null)
                skipFolder = x => false;

            List<Task<IEnumerable<FtpEntry>>> pending = new List<Task<IEnumerable<FtpEntry>>>();
            IEnumerable<FtpEntry> files = new List<FtpEntry>();

            using (var client = _getClient())
            using (var semaphore = new SemaphoreSlim(3))
            {
                Func<string, CancellationToken, Task<IEnumerable<FtpEntry>>> listFolderAsync = async (string path, CancellationToken ct) =>
                {
                    try
                    {
                        await semaphore.WaitAsync();

                        var retrier = Policy
                            .Handle<Exception>()
                            .WaitAndRetryAsync(new[]
                            {
                                TimeSpan.FromSeconds(1),
                                TimeSpan.FromSeconds(5),
                                TimeSpan.FromSeconds(15)
                            }, (ex, ts) =>
                            {
                                _logger.Warn(ex, "Failed to list folder {0}. Try again soon ...", path);
                            });
                        var res = await retrier.ExecuteAsync(ct1 =>
                        {
                            return Task.Run(() => _list(client, path), ct1);
                        }, ct).ConfigureAwait(false);
                        return res.Select(x => new FtpEntry
                        {
                            FullPath = x.Path.ToString(),
                            IsDirectory = x.Type == FtpEntryType.Directory,
                            Modified = x.Date,
                            Name = x.Name,
                            Size = x.Size.GetValueOrDefault(-1),
                        }).ToArray();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                };

                pending.Add(listFolderAsync(startPath, ctk));

                while (pending.Count > 0 && !ctk.IsCancellationRequested)
                {
                    var completedTask = await Task.WhenAny(pending).ConfigureAwait(false);
                    pending.Remove(completedTask);

                    // task could have completed with errors ... strange, let them progate.
                    var list = await completedTask.ConfigureAwait(false);

                    //we want to exclude folders . and .. that we dont want to search
                    foreach (var d in list.Where(x => x.IsDirectory && !x.Name.Equals(".") && !x.Name.Equals("..")))
                    {
                        if (skipFolder.Invoke(d))
                            _logger.Info("Skipping folder: {0}", d.FullPath);
                        else
                            pending.Add(listFolderAsync(d.FullPath, ctk));
                    }

                    files = files.Concat(list.Where(x => !x.IsDirectory));
                }

                ctk.ThrowIfCancellationRequested();

                return files;
            }
        }

        private ArxOne.Ftp.FtpClient _getClient()
        {
            return new ArxOne.Ftp.FtpClient(new Uri("ftp://" + this.Host), this.Credentials, new FtpClientParameters()
            {
                ConnectTimeout = TimeSpan.FromSeconds(60)
                
            });            
        }
        
    }
}
