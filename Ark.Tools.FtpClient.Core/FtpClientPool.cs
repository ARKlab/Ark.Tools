// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.Core
{
    public sealed class FtpClientPool : FtpClientWithConnectionBase, IFtpClientPool
    {
        public int PoolMaxSize { get; }

        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentStack<IFtpClientConnection> _pool;
        private readonly IFtpClientConnectionFactory _connectionFactory;

        public FtpClientPool(int poolMaxSize, string host, NetworkCredential credential, IFtpClientConnectionFactory connectionFactory) 
            : base(host, credential, poolMaxSize)
        {
            PoolMaxSize = poolMaxSize;
            _connectionFactory = connectionFactory;
            _semaphore = new SemaphoreSlim(poolMaxSize, poolMaxSize);
            _pool = new ConcurrentStack<IFtpClientConnection>();
        }

        protected override async Task<IFtpClientConnection> GetConnection(CancellationToken ctk = default)
        {
            IFtpClientConnection result = null;
            await _semaphore.WaitAsync(ctk);
            try
            {
                while (_pool.TryPop(out var candidate))
                {
                    if (await candidate.IsConnectedAsync(ctk))
                    {
                        result = candidate;
                        break;
                    }

                    candidate.Dispose();
                }

                if (result == null)
                {
                    result = _createNewConnection();
                    await result.ConnectAsync(ctk);
                }

                var pooled = new PooledFtpConnection(result);
                pooled.Disposing += _pooled_Disposing;
                return pooled;
            } catch
            {
                result?.Dispose();
                _semaphore.Release();
                throw;
            }
        }

        private void _pooled_Disposing(object sender, EventArgs e)
        {
            var pooled = sender as PooledFtpConnection;
            try
            {
                _pool.Push(pooled.Inner);                
            }
            catch
            {
                pooled.Inner.Dispose();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private IFtpClientConnection _createNewConnection()
        {
            return _connectionFactory.Create(Host, Credentials);
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        private void _dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    var a = _pool.ToArray();
                    foreach (var e in a)
                        e?.Dispose();

                    _semaphore?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            _dispose(true);
        }
        #endregion

        private sealed class PooledFtpConnection : IFtpClientConnection
        {
            private bool _disposedValue = false; // To detect redundant calls
            public IFtpClientConnection Inner { get; }
            public event EventHandler Disposing;

            public PooledFtpConnection(IFtpClientConnection inner)
            {
                Inner = inner;
            }

            public string Host => Inner.Host;

            public NetworkCredential Credentials => Inner.Credentials;


            public void Dispose()
            {
                if (!_disposedValue)
                {
                    Disposing?.Invoke(this, EventArgs.Empty);
                    _disposedValue = true;
                }
            }

            public ValueTask DisconnectAsync(CancellationToken ctk = default)
            {
                // we're trying to Pool these, so don't disconnect ...
                return default;
            }


            public Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default)
            {
                return Inner.DownloadFileAsync(path, ctk);
            }

            public Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default)
            {
                return Inner.ListDirectoryAsync(path, ctk);
            }

            public Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = null, Predicate<FtpEntry> skipFolder = null, CancellationToken ctk = default)
            {
                return Inner.ListFilesRecursiveAsync(startPath, skipFolder, ctk);
            }

            public Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
            {
                return Inner.UploadFileAsync(path, content, ctk);
            }

            public ValueTask ConnectAsync(CancellationToken ctk)
            {
                return Inner.ConnectAsync(ctk);
            }

            public ValueTask<bool> IsConnectedAsync(CancellationToken ctk = default)
            {
                return Inner.IsConnectedAsync(ctk);
            }
        }
    }
}