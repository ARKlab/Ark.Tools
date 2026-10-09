// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;
using Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface;
using Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface.Auth;
using Ark.Tools.Outbox;
using Ark.Tools.Rebus.Tests;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;

using Rebus.DataBus.InMem;
using Rebus.Transport.InMem;

using SimpleInjector;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>The WebRebus WebInterface started on a TestServer over an in-memory Rebus network and storage.</summary>
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
        var network = new InMemNetwork();
        var container = RebusHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        RebusHosting.Configure<ApiRebusHost>(
            container,
            t => t.UseDrainableInMemoryTransportAsOneWayClient(network),
            static d => d.StoreInMemory(new InMemDataStore()),
            startOutboxProcessor: false);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(WebRebusStartup).Assembly.GetName().Name,
            EnvironmentName = "IntegrationTests",
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseTestServer();
        var startup = WebRebusStartup.Create(builder, container);
        var app = builder.Build();
        startup.Configure(app);
        await app.StartAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        return new WebInterfaceTestHost(container, app);
    }

    /// <summary>Creates a TestServer client that sends a bearer with the given scopes.</summary>
    /// <param name="scopes">The scopes granted to the caller.</param>
    /// <returns>The authenticated client.</returns>
    public HttpClient CreateClient(params string[] scopes)
    {
        var client = App.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _createBearer(scopes));
        return client;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync().ConfigureAwait(false);
        await Container.DisposeAsync().ConfigureAwait(false);
    }

    private static string _createBearer(string[] scopes)
    {
        var token = new JwtSecurityToken(
            issuer: AuthConstants.IntegrationTestsDomain,
            audience: AuthConstants.IntegrationTestsAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "webrebus-test-user"),
                new Claim("scope", string.Join(' ', scopes)),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.IntegrationTestsEncryptionKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
