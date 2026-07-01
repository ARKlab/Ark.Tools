// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.FtpClient.Core;

using AwesomeAssertions;

using System.Net;
using System.Reflection;

namespace Ark.Tools.FtpClient.FluentFtp.Tests;

[TestClass]
public class FluentFtpClientFactoryTests
{
    [TestMethod]
    public void ClientFactory_ShouldInvokeHostAwareConfigCallback()
    {
        var callbackHost = string.Empty;
        var ftpClientFactory = new FluentFtpClientFactory((host, config) =>
        {
            callbackHost = host;
            config.SocketKeepAlive = false;
        });

        using var setup = CreateConnectionFromFactory(ftpClientFactory, "ftp://client-factory.example.com");
        var clientConfig = GetFluentFtpConfig(setup.Connection);

        callbackHost.Should().Be("client-factory.example.com");
        clientConfig.SocketKeepAlive.Should().BeFalse();
    }

    [TestMethod]
    public void PoolFactory_ShouldInvokeHostAwareConfigCallback()
    {
        var callbackHost = string.Empty;
        var ftpClientPoolFactory = new FluentFtpClientPoolFactory((host, config) =>
        {
            callbackHost = host;
            config.SocketKeepAlive = false;
        });

        using var setup = CreateConnectionFromFactory(ftpClientPoolFactory, "ftp://pool-factory.example.com");
        var clientConfig = GetFluentFtpConfig(setup.Connection);

        callbackHost.Should().Be("pool-factory.example.com");
        clientConfig.SocketKeepAlive.Should().BeFalse();
    }

    private static FluentFTP.FtpConfig GetFluentFtpConfig(FluentFtpClientConnection connection)
    {
        var clientField = typeof(FluentFtpClientConnection).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic);
        clientField.Should().NotBeNull();

        var client = clientField!.GetValue(connection);
        client.Should().NotBeNull();

        var configProperty = client!.GetType().GetProperty("Config", BindingFlags.Instance | BindingFlags.Public);
        configProperty.Should().NotBeNull();

        var config = configProperty!.GetValue(client) as FluentFTP.FtpConfig;
        config.Should().NotBeNull();
        return config!;
    }

    private static ConnectionSetup CreateConnectionFromFactory(FluentFtpClientFactory ftpClientFactory, string uri)
    {
        var connectionFactoryField = typeof(DefaultFtpClientFactory).GetField("_connectionFactory", BindingFlags.Instance | BindingFlags.NonPublic);
        connectionFactoryField.Should().NotBeNull();

        var connectionFactory = connectionFactoryField!.GetValue(ftpClientFactory) as IFtpClientConnectionFactory;
        connectionFactory.Should().NotBeNull();

        var ftpConfig = new FtpConfig(new Uri(uri), new NetworkCredential("user", "password"));
        var connection = connectionFactory!.Create(ftpConfig);
        return new ConnectionSetup(connection.Should().BeOfType<FluentFtpClientConnection>().Subject, ftpConfig);
    }

    private static ConnectionSetup CreateConnectionFromFactory(FluentFtpClientPoolFactory ftpClientPoolFactory, string uri)
    {
        var connectionFactoryField = typeof(DefaultFtpClientPoolFactory).GetField("_connectionFactory", BindingFlags.Instance | BindingFlags.NonPublic);
        connectionFactoryField.Should().NotBeNull();

        if (connectionFactoryField!.GetValue(ftpClientPoolFactory) is not IFtpClientConnectionFactory connectionFactory)
        {
            throw new InvalidOperationException("Expected _connectionFactory to be an IFtpClientConnectionFactory.");
        }

        var ftpConfig = new FtpConfig(new Uri(uri), new NetworkCredential("user", "password"));
        var connection = connectionFactory.Create(ftpConfig);
        return new ConnectionSetup(connection.Should().BeOfType<FluentFtpClientConnection>().Subject, ftpConfig);
    }

    private sealed class ConnectionSetup : IDisposable
    {
        public ConnectionSetup(FluentFtpClientConnection connection, FtpConfig ftpConfig)
        {
            Connection = connection;
            _ftpConfig = ftpConfig;
        }

        public FluentFtpClientConnection Connection { get; }

        private readonly FtpConfig _ftpConfig;

        public void Dispose()
        {
            Connection.Dispose();
            _ftpConfig.Dispose();
        }
    }
}
