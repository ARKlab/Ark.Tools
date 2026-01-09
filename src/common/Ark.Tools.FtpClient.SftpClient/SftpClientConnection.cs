// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using Renci.SshNet;
using Renci.SshNet.Sftp;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Ark.Tools.FtpClient.SftpClient;

public class SftpClientConnection : FtpClientConnectionBase
{

    private readonly Renci.SshNet.SftpClient _client;

    private readonly TimeSpan _keepAliveInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _operationTimeout = TimeSpan.FromMinutes(5);

    private bool _isDisposed;

    private const string _rsa = "1.2.840.113549.1.1.1";
    private const string _dsa = "1.2.840.10040.4.1";
    private const string _ecdsa = "1.2.840.10045.2.1";

    public SftpClientConnection(FtpConfig ftpConfig)
        : base(ftpConfig)
    {
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
    public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default)
    {
        path ??= "./";
        await _ensureConnected(ctk).ConfigureAwait(false);

        var rawLs = await _client.ListDirectoryAsync(path, ctk).ToListAsync(ctk).ConfigureAwait(false);
        return SftpClientConnection._parse(rawLs);

    }

    private async Task _ensureConnected(CancellationToken ctk)
    {
        if (!_client.IsConnected)
            await _client.ConnectAsync(ctk).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads the file asynchronous.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="ctk">The CTK.</param>
    /// <returns></returns>
    public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
    {
        await _ensureConnected(ctk).ConfigureAwait(false);
        using var ms = new MemoryStream(80 * 1024);
        await _client.DownloadAsync(path, ms, u => { }).ConfigureAwait(false);
        return ms.ToArray();
    }

    public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
    {
        await _ensureConnected(ctk).ConfigureAwait(false);
        using var ms = new MemoryStream(content);
        await _client.UploadAsync(ms, path).ConfigureAwait(false);
    }

    public override async Task DeleteDirectoryAsync(string path, CancellationToken ctk = default)
    {
        await _ensureConnected(ctk).ConfigureAwait(false);
        await _client.DeleteDirectoryAsync(path, ctk).ConfigureAwait(false);
    }

    public override async Task DeleteFileAsync(string path, CancellationToken ctk = default)
    {
        await _ensureConnected(ctk).ConfigureAwait(false);
        await _client.DeleteFileAsync(path, ctk).ConfigureAwait(false);
    }

    #region private helpers

    private ConnectionInfo _getConnectionInfo()
    {
        var connectionInfo = new ConnectionInfo(Uri.Host, Uri.Port, Credentials.UserName, new PasswordAuthenticationMethod(Credentials.UserName, Credentials.Password))
        {
            Timeout = TimeSpan.FromMinutes(5),
            RetryAttempts = 2
        };

        return connectionInfo;
    }

    private Renci.SshNet.SftpClient _getSFtpClientWithCertificate()
    {
        var connInfo = _getConnectionInfo();

        var cert = FtpConfig.ClientCertificate ?? throw new InvalidOperationException("ClientCertificate is not set");

        string? keyExchangeAlgorithm = null;
        byte[]? privateKeyBytes = null;
        string privateKeyPemString;
        bool isKeyNull = false;

        switch (cert.PublicKey.Oid.Value)
        {
            case _rsa:
                {
                    using RSA? rsaKey = cert.GetRSAPrivateKey();

                    keyExchangeAlgorithm = rsaKey?.KeyExchangeAlgorithm;
                    if (rsaKey != null)
                        privateKeyBytes = rsaKey.ExportRSAPrivateKey();
                    else
                        isKeyNull = true;
                    break;
                }
            case _dsa:
                {
                    using DSA? dsaKey = cert.GetDSAPrivateKey();

                    keyExchangeAlgorithm = dsaKey?.KeyExchangeAlgorithm;
                    if (dsaKey != null)
                        privateKeyBytes = dsaKey.ExportPkcs8PrivateKey();
                    else
                        isKeyNull = true;
                    break;
                }
            case _ecdsa:
                {
                    using ECDsa? ecdsaKey = cert.GetECDsaPrivateKey();

                    keyExchangeAlgorithm = ecdsaKey?.KeyExchangeAlgorithm;
                    if (ecdsaKey != null)
                        privateKeyBytes = ecdsaKey.ExportPkcs8PrivateKey();
                    else
                        isKeyNull = true;
                    break;
                }
            default:
                throw new NotSupportedException($"ClientCertificate does not support the given algorithm {cert.PublicKey.Oid.FriendlyName}");
        }

        if (isKeyNull)
            throw new InvalidOperationException($"ClientCertificate has a null Key");

        var privateKeyPem = PemEncoding.Write($"{keyExchangeAlgorithm} PRIVATE KEY", privateKeyBytes);
        privateKeyPemString = new string(privateKeyPem);

        var byteArray = Encoding.UTF8.GetBytes(privateKeyPemString);

        return new Renci.SshNet.SftpClient(connInfo.Host, connInfo.Username, new PrivateKeyFile[] { new PrivateKeyFile(new MemoryStream(byteArray)) })
        {
            KeepAliveInterval = _keepAliveInterval,
            OperationTimeout = _operationTimeout,
        };
    }

    private Renci.SshNet.SftpClient _getSFtpClient()
    {
        if (FtpConfig.ClientCertificate != null)
        {
            return _getSFtpClientWithCertificate();
        }
        else
        {
            var connInfo = _getConnectionInfo();

            return new Renci.SshNet.SftpClient(connInfo)
            {
                KeepAliveInterval = _keepAliveInterval,
                OperationTimeout = _operationTimeout,
            };
        }
    }

    private static List<FtpEntry> _parse(IEnumerable<ISftpFile> files)
    {
        var result = new List<FtpEntry>();

        foreach (var file in files)
        {
            var entry = new FtpEntry
            {
                FullPath = file.FullName,
                IsDirectory = file.IsDirectory,
                Modified = file.LastWriteTimeUtc,
                Name = file.Name,
                Size = file.Length
            };
            result.Add(entry);
        }
        return result;
    }

    public override async ValueTask ConnectAsync(CancellationToken ctk)
    {
        if (_client.IsConnected)
            return;

        await _client.ConnectAsync(ctk).ConfigureAwait(false);
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

        await Task.Run(() => _client.Disconnect(), ctk).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            _client?.Dispose();
        }

        _isDisposed = true;
    }

    #endregion private helpers

}