// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

using System.Security.Cryptography.X509Certificates;

namespace Ark.Tools.FtpClient.Core;

public class FtpConfig : IDisposable
{
    private bool _isDisposed;

    public Uri Uri { get; }

    #if NET10_0_OR_GREATER

    [UserCredentials]

    #endif
    public NetworkCredential? Credentials { get; }

    #if NET10_0_OR_GREATER

    [Secret]

    #endif
    public X509Certificate2? ClientCertificate { get; private set; }

    public FtpConfig(Uri uri, 
#if NET10_0_OR_GREATER
    [UserCredentials]
#endif
 NetworkCredential? credential = null, 
#if NET10_0_OR_GREATER
    [Secret]
#endif
 X509Certificate2? certificate = null)
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