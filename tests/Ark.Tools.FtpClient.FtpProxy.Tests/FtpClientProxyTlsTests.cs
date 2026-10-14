// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.FtpClient.Core;

using AwesomeAssertions;

using Flurl.Http;

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Ark.Tools.FtpClient.FtpProxy.Tests;

[TestClass]
public class FtpClientProxyTlsTests
{
    [TestMethod]
    public async Task DefaultConfig_ShouldRejectUntrustedServerCertificate()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (baseUri, serverTask) = _startTlsServer(cts.Token);

        using var ftpConfig = new FtpConfig(new Uri("ftp://ftp.example.com"), new NetworkCredential("user", "password"));
        using var proxy = new FtpClientProxy(new DefaultConfig(baseUri), ftpConfig);

        var act = async () => await _getClient(proxy).Request("v2", "ListFolder").GetAsync(cancellationToken: cts.Token).ConfigureAwait(false);

        var ex = await act.Should().ThrowAsync<FlurlHttpException>().ConfigureAwait(false);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>()
            .Which.InnerException.Should().BeOfType<AuthenticationException>();
        (await serverTask.ConfigureAwait(false)).Should().BeFalse();
    }

    [TestMethod]
    public async Task AcceptAnyServerCertificate_ShouldAcceptUntrustedServerCertificate()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (baseUri, serverTask) = _startTlsServer(cts.Token);

        using var ftpConfig = new FtpConfig(new Uri("ftp://ftp.example.com"), new NetworkCredential("user", "password"));
        using var proxy = new FtpClientProxy(new AcceptAnyConfig(baseUri), ftpConfig);

        using var response = await _getClient(proxy).Request("v2", "ListFolder").GetAsync(cancellationToken: cts.Token).ConfigureAwait(false);

        response.StatusCode.Should().Be(200);
        (await serverTask.ConfigureAwait(false)).Should().BeTrue();
    }

    // Serves a single HTTPS request with a self-signed certificate; the task result tells whether the client
    // accepted the certificate and sent its request. The task owns the listener so it is disposed only once the
    // exchange is over.
    private static (Uri BaseUri, Task<bool> ServerTask) _startTlsServer(CancellationToken ctk)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var baseUri = new Uri($"https://localhost:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        return (baseUri, _serveOnceAsync(listener, ctk));
    }

    // The client that talks to the proxy is private; reflecting it lets us exercise its real TLS handshake without
    // acquiring an OAuth token from an external identity provider.
    private static IFlurlClient _getClient(FtpClientProxy proxy)
    {
        var clientField = typeof(FtpClientProxy).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic);
        clientField.Should().NotBeNull();

        return clientField!.GetValue(proxy).Should().BeAssignableTo<IFlurlClient>().Subject;
    }

    private static X509Certificate2 _createSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());

        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Round-trip through PFX so SslStream can use the private key on every platform.
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null);
    }

    private static async Task<bool> _serveOnceAsync(TcpListener listener, CancellationToken ctk)
    {
        using var ownedListener = listener;
        using var cert = _createSelfSignedCertificate();
        using var tcp = await listener.AcceptTcpClientAsync(ctk).ConfigureAwait(false);
        var ssl = new SslStream(tcp.GetStream());
        await using var sslScope = ssl.ConfigureAwait(false);
        try
        {
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = cert }, ctk).ConfigureAwait(false);

            // With TLS 1.3 the server completes its handshake before the client validates the certificate,
            // so a rejected certificate shows up as no request being received.
            using var reader = new StreamReader(ssl, Encoding.ASCII, leaveOpen: true);
            if (await reader.ReadLineAsync(ctk).ConfigureAwait(false) is null)
                return false;

            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(ctk).ConfigureAwait(false)))
            {
            }

            var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n[]");
            await ssl.WriteAsync(response, ctk).ConfigureAwait(false);
            await ssl.FlushAsync(ctk).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException)
        {
            return false;
        }
    }

    private class DefaultConfig : IFtpClientProxyConfig
    {
        public DefaultConfig(Uri baseUri)
        {
            FtpProxyWebInterfaceBaseUri = baseUri;
        }

        public string ClientID => "client";
        public string ClientKey => "key";
        public Uri FtpProxyWebInterfaceBaseUri { get; }
        public string ApiIdentifier => "api";
        public string TenantID => "tenant";
        public bool UseAuth0 => false;
        public int? ListingDegreeOfParallelism => null;
    }

    private sealed class AcceptAnyConfig : DefaultConfig, IFtpClientProxyConfig
    {
        public AcceptAnyConfig(Uri baseUri)
            : base(baseUri)
        {
        }

        public bool AcceptAnyServerCertificate => true;
    }
}
