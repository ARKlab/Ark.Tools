// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using ArxOne.Ftp;

using Org.Mentalis.Network.ProxySocket;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Designed to be overridden")]
public class FtpClientPoolArxOne : FtpClientBase, IFtpClientPool
{
    private readonly ArxOne.Ftp.FtpClient _client;
    private readonly SemaphoreSlim _semaphore;
    private readonly ISocksConfig? _socksConfig;
    private readonly Action<FtpClientParameters>? _configurer;

    private bool _isDisposed;

    public FtpClientPoolArxOne(int maxPoolSize, FtpConfig ftpConfig)
        : base(ftpConfig, maxPoolSize)
    {
        _semaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);
        _client = _getClient();
    }

    public FtpClientPoolArxOne(ISocksConfig socksConfig, int maxPoolSize, FtpConfig ftpConfig)
        : base(ftpConfig, maxPoolSize)
    {
        _socksConfig = socksConfig;
        _semaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);
        _client = _getClient();
    }

    public FtpClientPoolArxOne(IArxOneConfig arxOneConfig, int maxPoolSize, FtpConfig ftpConfig)
        : base(ftpConfig, maxPoolSize)
    {
        _socksConfig = arxOneConfig.SocksConfig;
        _configurer = arxOneConfig.Configurer;
        _semaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);
        _client = _getClient();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "Socket must be configured sync")]
    private protected virtual ArxOne.Ftp.FtpClient _getClient()
    {
        var ftpClientParameters = new FtpClientParameters()
        {
            ConnectTimeout = TimeSpan.FromSeconds(60),
            ReadWriteTimeout = TimeSpan.FromMinutes(3),
            Passive = true,
        };

        if (_socksConfig != null)
        {
            ftpClientParameters.ProxyConnect = e =>
            {
                var s = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    ProxyEndPoint = new IPEndPoint(IPAddress.Parse(_socksConfig.IpAddress), _socksConfig.Port),
                    ProxyUser = _socksConfig.UserName,
                    ProxyPass = _socksConfig.Password,
                    ProxyType = _socksConfig.Type
                };

                switch (e)
                {
                    case DnsEndPoint dns:
                        s.Connect(dns.Host, dns.Port);
                        break;
                    case IPEndPoint ip:
                        s.Connect(ip);
                        break;

                    default: throw new NotSupportedException();
                }

                return s;
            };
        }

        if (_configurer != null)
            _configurer(ftpClientParameters);

        return new ArxOne.Ftp.FtpClient(this.Uri, this.Credentials, ftpClientParameters);
    }

    public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
    {
        await _semaphore.WaitAsync(ctk).ConfigureAwait(false);
        try
        {
            var istrm = _client.Retr(path, FtpTransferMode.Binary);
            await using (istrm.ConfigureAwait(false))
            {
                using var ms = new MemoryStream(81920);
                await istrm.CopyToAsync(ms, 81920, ctk).ConfigureAwait(false);
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
        await _semaphore.WaitAsync(ctk).ConfigureAwait(false);
        try
        {
            var ostrm = _client.Stor(path, FtpTransferMode.Binary);
            await using (ostrm.ConfigureAwait(false))
            {
                await ostrm.WriteAsync(content, ctk).ConfigureAwait(false);
                await ostrm.FlushAsync(ctk).ConfigureAwait(false);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override async Task DeleteFileAsync(string path, CancellationToken ctk = default)
    {
        await _semaphore.WaitAsync(ctk).ConfigureAwait(false);
        try
        {
            _client.Delete(path, false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override async Task DeleteDirectoryAsync(string path, CancellationToken ctk = default)
    {
        await _semaphore.WaitAsync(ctk).ConfigureAwait(false);
        try
        {
            _client.Delete(path, true);
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

    public override async Task<IEnumerable<Core.FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default)
    {
        path ??= "./";

        await _semaphore.WaitAsync(ctk).ConfigureAwait(false);
        try
        {
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
        if (_isDisposed) return;

        if (disposing)
        {
            _client?.Dispose();
            _semaphore?.Dispose();
        }

        _isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
