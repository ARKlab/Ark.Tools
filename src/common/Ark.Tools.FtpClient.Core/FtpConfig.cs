// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.Core(net10.0)', Before:
namespace Ark.Tools.FtpClient.Core
{
    public class FtpConfig : IDisposable
    {
        private bool _isDisposed;

        public Uri Uri { get; }
        public NetworkCredential? Credentials { get; }

        public X509Certificate2? ClientCertificate { get; private set; }

        public FtpConfig(Uri uri, NetworkCredential? credential = null, X509Certificate2? certificate = null)
        {
            Uri = uri;
            Credentials = credential;
            ClientCertificate = certificate;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
                ClientCertificate?.Dispose();

            _isDisposed = true;
        }
=======
namespace Ark.Tools.FtpClient.Core;

public class FtpConfig : IDisposable
{
    private bool _isDisposed;

    public Uri Uri { get; }
    public NetworkCredential? Credentials { get; }

    public X509Certificate2? ClientCertificate { get; private set; }

    public FtpConfig(Uri uri, NetworkCredential? credential = null, X509Certificate2? certificate = null)
    {
        Uri = uri;
        Credentials = credential;
        ClientCertificate = certificate;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
            ClientCertificate?.Dispose();

        _isDisposed = true;
>>>>>>> After


namespace Ark.Tools.FtpClient.Core;

    public class FtpConfig : IDisposable
    {
        private bool _isDisposed;

        public Uri Uri { get; }
        public NetworkCredential? Credentials { get; }

        public X509Certificate2? ClientCertificate { get; private set; }

        public FtpConfig(Uri uri, NetworkCredential? credential = null, X509Certificate2? certificate = null)
        {
            Uri = uri;
            Credentials = credential;
            ClientCertificate = certificate;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
                ClientCertificate?.Dispose();

            _isDisposed = true;
        }
    }