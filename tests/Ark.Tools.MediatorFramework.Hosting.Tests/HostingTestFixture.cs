// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.MessagePackFormatter;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.Hosting.Contracts;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Nodatime.Protobuf;
using Ark.Tools.Rebus;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;

using MessagePack.Resolvers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.OpenApi;

using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using NodaTime;

using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>
/// Owns the synthetic mediator container and independently built transport hosts.
/// </summary>
public sealed class HostingTestFixture : IAsyncDisposable
{
    private readonly InMemNetwork _network = new();
    private readonly List<WebApplication> _hosts = [];
    private bool _rebusConfigured;
    private bool _disposed;

    /// <summary>Initializes a fixture with deterministic handlers and test-only identity.</summary>
    public HostingTestFixture()
    {
        Container = new Container();
        Container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        State = new HostingTestState();
        PrincipalProvider = new TestPrincipalProvider();

        Container.RegisterInstance(State);
        Container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(PrincipalProvider);
        Container.Register<IRequestHandler<HostingRequest, HostingResponse>, HostingRequestHandler>();
        Container.Register<IQueryHandler<HostingQuery, HostingResponse>, HostingQueryHandler>();
        Container.Register<ICommandHandler<HostingCommand>, HostingCommandHandler>();
        Container.Register<ICommandHandler<HostingRebusCommand>, HostingRebusCommandHandler>();
        Container.Register<IRequestHandler<HostingValidationRequest, HostingResponse>, HostingValidationHandler>();
        Container.Register<IRequestHandler<HostingStatusRequest, HostingResponse>, HostingStatusHandler>();
        Container.Register<IQueryHandler<HostingNotFoundQuery, HostingResponse>, HostingNotFoundHandler>();
        Container.Register<IRequestHandler<HostingBusinessViolationRequest, HostingResponse>, HostingBusinessViolationHandler>();
        Container.Register<IRequestHandler<HostingUnexpectedRequest, HostingResponse>, HostingUnexpectedHandler>();
        Container.Register<IQueryHandler<HostingAuthorizedQuery, HostingResponse>, HostingAuthorizedHandler>();
        Container.Register<IQueryHandler<HostingUserContextQuery, HostingResponse>, HostingUserContextHandler>();
        Container.Register<IRequestHandler<HostingETagMismatchRequest, HostingResponse>, HostingETagMismatchHandler>();
        Container.Register<IRequestHandler<HostingOptimisticConcurrencyRequest, HostingResponse>, HostingOptimisticConcurrencyHandler>();
        Container.Register<IQueryHandler<HostingStreamQuery, IAsyncEnumerable<HostingStreamItem>>, HostingStreamHandler>();
        Container.Register<IRequestHandler<HostingAttachmentUploadRequest, HostingResponse>, HostingAttachmentUploadHandler>();
        Container.Register<IRequestHandler<HostingAttachmentCollectionUploadRequest, HostingResponse>, HostingAttachmentCollectionUploadHandler>();
        Container.Register<IQueryHandler<HostingAttachmentDownloadQuery, Ark.MediatorFramework.IArkAttachment>, HostingAttachmentDownloadHandler>();
        Container.Register<IQueryHandler<HostingOpenApiQuery, HostingOpenApiResponse>, HostingOpenApiHandler>();
        Container.Register<IQueryHandler<HostingWireTypesQuery, HostingWireTypesResponse>, HostingWireTypesHandler>();
        Container.Register<IQueryHandler<HostingVersionedQuery, HostingResponse>, HostingVersionedHandler>();

        Container.RegisterAuthorization();
        Container.RegisterAuthorizationPolicy<HostingScopePolicy>();
        HostingEndpointMappings.RegisterRebusHandlers(Container);
        Container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));
    }

    /// <summary>Gets the SimpleInjector container used by all synthetic hosts.</summary>
    public Container Container { get; }

    /// <summary>Gets deterministic state updated by synthetic handlers.</summary>
    public HostingTestState State { get; }

    /// <summary>Gets the mutable test-only principal provider used by the HTTP host.</summary>
    internal TestPrincipalProvider PrincipalProvider { get; }

    /// <summary>Gets whether the fixture has disposed its hosts and container.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>Gets the shared in-memory network used by the Rebus host.</summary>
    public InMemNetwork Network => _network;

    /// <summary>
    /// Waits until every non-error queue in the in-memory network is empty, or throws
    /// <see cref="TimeoutException"/> if <paramref name="timeout"/> elapses first.
    /// </summary>
    public async Task WaitForIdleAsync(TimeSpan? timeout = null, CancellationToken ctk = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctk);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));

        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();

            var pending = _network.Queues
                .Where(q => !string.Equals(q, "error", StringComparison.OrdinalIgnoreCase))
                .Sum(q => _network.GetCount(q));

            if (pending == 0)
                return;

            await Task.Delay(50, cts.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Builds and maps an independent Minimal API host.</summary>
    /// <returns>The unstarted Minimal API application.</returns>
    public WebApplication BuildMinimalApiHost()
    {
        ThrowIfDisposed();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Container);
        builder.Services.AddSingleton(PrincipalProvider);
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                static _ => { });
        builder.Services.AddArkMinimalApiHost(Container, options =>
        {
            options.RequireAuthenticatedUser = false;
            options.CrossWireContainer = (container, services) =>
                container.RegisterInstance(services.GetRequiredService<IHttpContextAccessor>());
        });
        builder.Services.AddArkProblemDetailsExceptionHandler();
        builder.Services.AddMessagePackFormatter(StandardResolver.Instance);
        builder.Services.AddOpenApi("v1", ConfigureOpenApi);
        builder.Services.AddOpenApi("v2", ConfigureOpenApi);
        var app = builder.Build();
        app.UseArkProblemDetailsExceptionHandler();
        app.UseArkMinimalApiHost(Container);
        app.Use(async (context, next) =>
        {
            var sizeLimit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
            if (sizeLimit is not null && context.Request.ContentLength > sizeLimit)
            {
                context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
                return;
            }

            await next().ConfigureAwait(false);
        });
        HostingEndpointMappings.MapMinimalApi(app);
        app.MapOpenApi().AllowAnonymous();
        _hosts.Add(app);
        return app;
    }

    /// <summary>Builds and starts an independent Minimal API test host.</summary>
    /// <returns>The started Minimal API application.</returns>
    public async Task<WebApplication> StartMinimalApiHostAsync()
    {
        var app = BuildMinimalApiHost();
        await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
        return app;
    }

    /// <summary>Builds and maps an independent code-first gRPC host.</summary>
    /// <returns>The unstarted gRPC application.</returns>
    public WebApplication BuildGrpcHost()
    {
        ThrowIfDisposed();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                static _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCodeFirstGrpc(options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());
        builder.Services.AddCodeFirstGrpcReflection();
        builder.Services.AddSingleton(Container);
        builder.Services.AddSingleton(PrincipalProvider);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (_, next) =>
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(Container).ConfigureAwait(false);
            await next().ConfigureAwait(false);
        });
        RuntimeTypeModel.Default.AddNodaTimeSurrogates();
        HostingEndpointMappings.MapGrpc(app);
        app.MapCodeFirstGrpcReflectionService().AllowAnonymous();
        _hosts.Add(app);
        return app;
    }

    /// <summary>Builds and starts an independent gRPC test host.</summary>
    /// <returns>The started gRPC application.</returns>
    public async Task<WebApplication> StartGrpcHostAsync()
    {
        var app = BuildGrpcHost();
        await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
        return app;
    }

    /// <summary>Builds an isolated in-memory Rebus bus for the synthetic messages.</summary>
    /// <returns>The started Rebus bus.</returns>
    public IBus BuildRebusHost()
    {
        ThrowIfDisposed();
        if (!_rebusConfigured)
        {
            Container.ConfigureRebus(config =>
            {
                config.Transport(transport => transport.UseInMemoryTransport(_network, "hosting"));
                config.Routing(HostingEndpointMappings.ConfigureRebusRouting);
            });
            _rebusConfigured = true;
        }

        return Container.GetInstance<IBus>();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (var index = _hosts.Count - 1; index >= 0; index--)
            await _hosts[index].DisposeAsync().ConfigureAwait(false);
        await Container.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void ConfigureOpenApi(OpenApiOptions options)
    {
        options
            .AddArkServerSetProperties()
            .AddArkXmlDocumentation()
            .AddArkNodaTimeSchemas()
            .AddArkPolymorphism<HostingShape, HostingShapeKind>(
                "kind",
                (HostingShapeKind.Circle, typeof(HostingCircle)));
    }
}

/// <summary>Deterministic state shared by synthetic mediator handlers.</summary>
public sealed class HostingTestState
{
    private readonly TaskCompletionSource _commandExecution = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the number of request handler executions.</summary>
    public int RequestExecutions { get; internal set; }

    /// <summary>Gets whether a generated request supplied a cancellable token.</summary>
    public bool RequestCancellationTokenWasCancelable { get; internal set; }

    /// <summary>Gets the server-owned stamp received by the request handler.</summary>
    public string? LastRequestServerStamp { get; internal set; }

    /// <summary>Gets the number of command handler executions.</summary>
    public int CommandExecutions { get; internal set; }

    /// <summary>Gets a task that completes when a command handler executes.</summary>
    public Task CommandExecuted => _commandExecution.Task;

    /// <summary>Gets the name of the last uploaded attachment.</summary>
    public string? LastAttachmentName { get; internal set; }

    /// <summary>Gets the content read by the last single-file upload handler.</summary>
    public string? LastAttachmentContent { get; internal set; }

    /// <summary>Gets the number of files received by the last collection upload handler.</summary>
    public int LastAttachmentCount { get; internal set; }

    /// <summary>Gets the number of times an authorized handler executed.</summary>
    public int AuthorizedExecutions { get; internal set; }

    /// <summary>Gets or sets whether the stream producer waits after its first item.</summary>
    public bool HoldStreamAfterFirst { get; set; }

    /// <summary>Gets a task that completes when the stream producer yields its first item.</summary>
    public Task StreamFirstItemProduced => _streamFirstItem.Task;

    /// <summary>Gets a task that completes when stream cancellation reaches the producer.</summary>
    public Task StreamCancellationObserved => _streamCancellation.Task;

    private readonly TaskCompletionSource _streamFirstItem = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _streamRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _streamCancellation = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void RecordCommandExecution()
    {
        CommandExecutions++;
        _commandExecution.TrySetResult();
    }

    internal void RecordFirstStreamItem()
    {
        _streamFirstItem.TrySetResult();
    }

    internal void RecordStreamCancellation()
    {
        _streamCancellation.TrySetResult();
    }

    internal void ReleaseStream()
    {
        _streamRelease.TrySetResult();
    }

    internal Task WaitForStreamReleaseAsync(CancellationToken ctk)
    {
        return _streamRelease.Task.WaitAsync(ctk);
    }
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
        _state.RequestCancellationTokenWasCancelable = ctk.CanBeCanceled;
        _state.LastRequestServerStamp = request.ServerStamp;
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

internal sealed class HostingStatusHandler : IRequestHandler<HostingStatusRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingStatusRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingResponse
        {
            Message = request.Value,
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingNotFoundHandler : IQueryHandler<HostingNotFoundQuery, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingNotFoundQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return null!;
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
        _state.RecordCommandExecution();
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
        _state.RecordCommandExecution();
    }
}

internal sealed class HostingValidationHandler : IRequestHandler<HostingValidationRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingValidationRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new FluentValidation.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(nameof(HostingValidationRequest.Value), "The synthetic value is invalid."),
            ]);
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
            Status = 422,
        };
        throw new Ark.Tools.Core.BusinessRuleViolation.BusinessRuleViolationException(violation);
    }
}

internal sealed class HostingUnexpectedHandler : IRequestHandler<HostingUnexpectedRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingUnexpectedRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new InvalidOperationException("The synthetic handler failed unexpectedly.");
    }
}

internal sealed class HostingAuthorizedHandler : IQueryHandler<HostingAuthorizedQuery, HostingResponse>
{
    private readonly HostingTestState _state;

    public HostingAuthorizedHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingAuthorizedQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.AuthorizedExecutions++;
        return new HostingResponse
        {
            Message = "authorized",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingUserContextHandler : IQueryHandler<HostingUserContextQuery, HostingResponse>
{
    private readonly IContextProvider<ClaimsPrincipal> _principalProvider;

    public HostingUserContextHandler(IContextProvider<ClaimsPrincipal> principalProvider)
    {
        _principalProvider = principalProvider;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingUserContextQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingResponse
        {
            Message = _principalProvider.Current.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingETagMismatchHandler : IRequestHandler<HostingETagMismatchRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(HostingETagMismatchRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new Ark.Tools.Core.EntityTag.EntityTagMismatchException("The synthetic ETag does not match.");
    }
}

internal sealed class HostingOptimisticConcurrencyHandler : IRequestHandler<HostingOptimisticConcurrencyRequest, HostingResponse>
{
    public async Task<HostingResponse> ExecuteAsync(
        HostingOptimisticConcurrencyRequest request,
        CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new Ark.Tools.Core.OptimisticConcurrencyException("The synthetic entity was concurrently modified.");
    }
}

internal sealed class HostingStreamHandler : IQueryHandler<HostingStreamQuery, IAsyncEnumerable<HostingStreamItem>>
{
    private readonly HostingTestState _state;

    public HostingStreamHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<IAsyncEnumerable<HostingStreamItem>> ExecuteAsync(HostingStreamQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return Enumerate(query.Count, _state, ctk);
    }

    private static async IAsyncEnumerable<HostingStreamItem> Enumerate(
        int count,
        HostingTestState state,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ctk)
    {
        for (var number = 1; number <= count; number++)
        {
            ctk.ThrowIfCancellationRequested();
            if (number == 1)
                state.RecordFirstStreamItem();
            yield return new HostingStreamItem { Number = number };
            if (number == 1 && state.HoldStreamAfterFirst)
            {
                try
                {
                    await state.WaitForStreamReleaseAsync(ctk).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    state.RecordStreamCancellation();
                    throw;
                }
            }
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
        if (request.Attachment is not null)
        {
            using var reader = new StreamReader(request.Attachment.OpenRead(), Encoding.UTF8);
            _state.LastAttachmentContent = await reader.ReadToEndAsync(ctk).ConfigureAwait(false);
            _state.LastAttachmentName = request.Attachment.Name;
        }
        return new HostingResponse
        {
            Message = request.Attachment?.Name ?? "none",
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingAttachmentCollectionUploadHandler : IRequestHandler<HostingAttachmentCollectionUploadRequest, HostingResponse>
{
    private readonly HostingTestState _state;

    public HostingAttachmentCollectionUploadHandler(HostingTestState state)
    {
        _state = state;
    }

    public async Task<HostingResponse> ExecuteAsync(HostingAttachmentCollectionUploadRequest request, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _state.LastAttachmentCount = request.Attachments.Count;
        return new HostingResponse
        {
            Message = string.Join(",", request.Attachments.Select(attachment => attachment.Name)),
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingAttachmentDownloadHandler : IQueryHandler<HostingAttachmentDownloadQuery, Ark.MediatorFramework.IArkAttachment>
{
    public async Task<Ark.MediatorFramework.IArkAttachment> ExecuteAsync(
        HostingAttachmentDownloadQuery query,
        CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (!string.Equals(query.Name, "download.txt", StringComparison.Ordinal))
            return null!;

        return new Ark.MediatorFramework.ArkAttachment(
            query.Name,
            "text/plain",
            () => new MemoryStream(Encoding.UTF8.GetBytes("downloaded content")));
    }
}

internal sealed class HostingOpenApiHandler : IQueryHandler<HostingOpenApiQuery, HostingOpenApiResponse>
{
    public async Task<HostingOpenApiResponse> ExecuteAsync(HostingOpenApiQuery query, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingOpenApiResponse
        {
            Date = new NodaTime.LocalDate(2026, 8, 6),
            Shape = new HostingCircle { Radius = 3 },
            ServerStamp = "hosting-server",
        };
    }
}

internal sealed class HostingWireTypesHandler : IQueryHandler<HostingWireTypesQuery, HostingWireTypesResponse>
{
    public async Task<HostingWireTypesResponse> ExecuteAsync(
        HostingWireTypesQuery query,
        CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new HostingWireTypesResponse
        {
            Date = new LocalDate(2026, 8, 6),
            DateTime = new LocalDateTime(2026, 8, 6, 15, 44),
            Shape = new HostingCircle { Radius = 7 },
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
            Message = $"{query.Id}:{query.Value ?? "versioned"}",
            ServerStamp = "hosting-server",
        };
    }
}
