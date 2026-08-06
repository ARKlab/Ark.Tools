// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;
using Ark.Tools.Rebus;
using Ark.Tools.Solid;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using ProtoBuf.Grpc.Server;

using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>
/// Owns the synthetic mediator container and independently built transport hosts.
/// </summary>
public sealed class HostingTestFixture : IAsyncDisposable
{
    private readonly InMemNetwork _network = new();
    private readonly List<WebApplication> _hosts = [];
    private IBus? _bus;
    private bool _disposed;

    /// <summary>Initializes a fixture with deterministic handlers and test-only identity.</summary>
    public HostingTestFixture()
    {
        Container = new Container();
        Container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        State = new HostingTestState();

        Container.RegisterInstance(State);
        Container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, TestPrincipalProvider>();
        Container.Register<IRequestHandler<HostingRequest, HostingResponse>, HostingRequestHandler>();
        Container.Register<IQueryHandler<HostingQuery, HostingResponse>, HostingQueryHandler>();
        Container.Register<ICommandHandler<HostingCommand>, HostingCommandHandler>();
        Container.Register<ICommandHandler<HostingRebusCommand>, HostingRebusCommandHandler>();
        Container.Register<IRequestHandler<HostingValidationRequest, HostingResponse>, HostingValidationHandler>();
        Container.Register<IRequestHandler<HostingBusinessViolationRequest, HostingResponse>, HostingBusinessViolationHandler>();
        Container.Register<IQueryHandler<HostingStreamQuery, IAsyncEnumerable<HostingStreamItem>>, HostingStreamHandler>();
        Container.Register<IRequestHandler<HostingAttachmentUploadRequest, HostingResponse>, HostingAttachmentUploadHandler>();
        Container.Register<IQueryHandler<HostingVersionedQuery, HostingResponse>, HostingVersionedHandler>();

        HostingEndpointMappings.RegisterRebusHandlers(Container);
        Container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));
    }

    /// <summary>Gets the SimpleInjector container used by all synthetic hosts.</summary>
    public Container Container { get; }

    /// <summary>Gets deterministic state updated by synthetic handlers.</summary>
    public HostingTestState State { get; }

    /// <summary>Gets whether the fixture has disposed its hosts and container.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>Builds and maps an independent Minimal API host.</summary>
    /// <returns>The unstarted Minimal API application.</returns>
    public WebApplication BuildMinimalApiHost()
    {
        ThrowIfDisposed();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Container);
        var app = builder.Build();
        HostingEndpointMappings.MapMinimalApi(app);
        _hosts.Add(app);
        return app;
    }

    /// <summary>Builds and maps an independent code-first gRPC host.</summary>
    /// <returns>The unstarted gRPC application.</returns>
    public WebApplication BuildGrpcHost()
    {
        ThrowIfDisposed();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddCodeFirstGrpc();
        builder.Services.AddSingleton(Container);
        var app = builder.Build();
        HostingEndpointMappings.MapGrpc(app);
        _hosts.Add(app);
        return app;
    }

    /// <summary>Builds an isolated in-memory Rebus bus for the synthetic messages.</summary>
    /// <returns>The started Rebus bus.</returns>
    public IBus BuildRebusHost()
    {
        ThrowIfDisposed();
        if (_bus is not null)
            return _bus;

        Container.ConfigureRebus(config =>
        {
            config.Transport(transport => transport.UseInMemoryTransport(_network, "hosting-test"));
            config.Routing(HostingEndpointMappings.ConfigureRebusRouting);
        });
        _bus = Container.GetInstance<IBus>();
        return _bus;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (var index = _hosts.Count - 1; index >= 0; index--)
            await _hosts[index].DisposeAsync().ConfigureAwait(false);
        Container.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>Deterministic state shared by synthetic mediator handlers.</summary>
public sealed class HostingTestState
{
    /// <summary>Gets the number of request handler executions.</summary>
    public int RequestExecutions { get; internal set; }

    /// <summary>Gets the number of command handler executions.</summary>
    public int CommandExecutions { get; internal set; }

    /// <summary>Gets the name of the last uploaded attachment.</summary>
    public string? LastAttachmentName { get; internal set; }
}

internal sealed class HostingRequestHandler : IRequestHandler<HostingRequest, HostingResponse>
{
    private readonly HostingTestState _state;

    public HostingRequestHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.RequestExecutions++;
        return new HostingResponse
        {
            Message = $"{request.Id}:{request.Filter}:{request.Value}",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingQueryHandler : IQueryHandler<HostingQuery, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingResponse
        {
            Message = $"{query.Id}:{query.Value}",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingCommandHandler : ICommandHandler<HostingCommand>
{
    private readonly HostingTestState _state;

    public HostingCommandHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task ExecuteAsync(HostingCommand command, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.CommandExecutions++;
    }
}

internal sealed class HostingRebusCommandHandler : ICommandHandler<HostingRebusCommand>
{
    private readonly HostingTestState _state;

    public HostingRebusCommandHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task ExecuteAsync(HostingRebusCommand command, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.CommandExecutions++;
    }
}

internal sealed class HostingValidationHandler : IRequestHandler<HostingValidationRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingValidationRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new FluentValidation.ValidationException("The synthetic value is invalid.");
    }
}

internal sealed class HostingBusinessViolationHandler : IRequestHandler<HostingBusinessViolationRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingBusinessViolationRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        var violation = new Ark.Tools.Core.BusinessRuleViolation.BusinessRuleViolation("Synthetic rule")
        {
            Detail = "The synthetic business rule was violated.",
        };
        throw new Ark.Tools.Core.BusinessRuleViolation.BusinessRuleViolationException(violation);
    }
}

internal sealed class HostingStreamHandler : IQueryHandler<HostingStreamQuery, IAsyncEnumerable<HostingStreamItem>>
{
    public async Task<IAsyncEnumerable<HostingStreamItem>> ExecuteAsync(HostingStreamQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return Enumerate(query.Count, ctk);
    }

    private static async IAsyncEnumerable<HostingStreamItem> Enumerate(
        int count,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ctk)
    {
        for (var number = 1; number <= count; number++)
        {
            ctk.ThrowIfCancellationRequested();
            yield return new HostingStreamItem { Number = number };
            await Task.Yield();
        }
    }
}

internal sealed class HostingAttachmentUploadHandler : IRequestHandler<HostingAttachmentUploadRequest, HostingResponse>
{
    private readonly HostingTestState _state;

    public HostingAttachmentUploadHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingAttachmentUploadRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.LastAttachmentName = request.Attachment?.Name;
        return new HostingResponse
        {
            Message = request.Attachment?.Name ?? "none",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingVersionedHandler : IQueryHandler<HostingVersionedQuery, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingVersionedQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingResponse
        {
            Message = query.Value ?? "versioned",
            ServerStamp = "hosting-server",
        };
    }
}
