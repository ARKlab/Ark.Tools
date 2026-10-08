// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.MediatorFramework.Sample.Core.Web.WebInterface;
using Ark.MediatorFramework.Sample.Core.Web.WebInterface.Auth;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;

using NodaTime;

using SimpleInjector;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>The Web variant's WebInterface started on a TestServer over in-memory messaging and storage.</summary>
internal sealed class WebInterfaceTestHost : IAsyncDisposable
{
    private WebInterfaceTestHost(Container container, WebApplication app)
    {
        Container = container;
        App = app;
    }

    /// <summary>Gets the application container.</summary>
    public Container Container { get; }

    /// <summary>Gets the started web application.</summary>
    public WebApplication App { get; }

    /// <summary>Composes the production WebInterface over in-memory seams and starts it.</summary>
    /// <returns>The started host.</returns>
    public static async Task<WebInterfaceTestHost> StartAsync()
    {
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var transport = new InMemoryMessagingTransport();
        var container = WebHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SampleHost).Assembly.GetName().Name,
            EnvironmentName = "IntegrationTests",
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseTestServer();
        // The attachment lifetime must outlive the network's one-hour maximum scheduling delay.
        var dataBus = new InMemoryMessagingDataBus(SystemClock.Instance, Duration.FromHours(2));
        var startup = SampleHost.Configure(builder, container, transport, dataBus, resourceManagement: null);
        var app = builder.Build();
        startup.Configure(app);
        await app.StartAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        return new WebInterfaceTestHost(container, app);
    }

    /// <summary>Creates a bearer token accepted by the integration-test authentication scheme.</summary>
    /// <param name="scopes">The scopes granted to the caller.</param>
    /// <returns>The serialized token.</returns>
    public static string CreateBearer(params string[] scopes)
    {
        var token = new JwtSecurityToken(
            issuer: AuthConstants.IntegrationTestsDomain,
            audience: AuthConstants.IntegrationTestsAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "web-test-user"),
                new Claim("scope", string.Join(' ', scopes)),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.IntegrationTestsEncryptionKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates a TestServer client that sends a bearer with the given scopes.</summary>
    /// <param name="scopes">The scopes granted to the caller.</param>
    /// <returns>The authenticated client.</returns>
    public HttpClient CreateClient(params string[] scopes)
    {
        var client = App.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateBearer(scopes));
        return client;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync().ConfigureAwait(false);
        await Container.DisposeAsync().ConfigureAwait(false);
    }
}
