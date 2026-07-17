// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using ProtoBuf.Grpc.Server;

using SimpleInjector;

using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Grpc;

/// <summary>Provides the authenticated principal for the current gRPC call.</summary>
public sealed class ArkGrpcUserContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Initializes a new instance of the <see cref="ArkGrpcUserContextProvider"/> class.</summary>
    /// <param name="httpContextAccessor">The ASP.NET Core HTTP context accessor.</param>
    public ArkGrpcUserContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public ClaimsPrincipal Current
        => ArkGrpcUserContext.Current
            ?? _httpContextAccessor.HttpContext?.User
            ?? new ClaimsPrincipal(new ClaimsIdentity());
}

/// <summary>Holds the authenticated principal while a gRPC call is executing.</summary>
public static class ArkGrpcUserContext
{
    private static readonly AsyncLocal<ClaimsPrincipal?> Principal = new();

    /// <summary>Gets the principal associated with the current gRPC call, if any.</summary>
    public static ClaimsPrincipal? Current => Principal.Value;

    internal static IDisposable Push(ClaimsPrincipal principal)
    {
        var previous = Principal.Value;
        Principal.Value = principal;
        return new Restore(previous);
    }

    private sealed class Restore : IDisposable
    {
        private readonly ClaimsPrincipal? _previous;
        private bool _disposed;

        public Restore(ClaimsPrincipal? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Principal.Value = _previous;
            _disposed = true;
        }
    }
}

/// <summary>Registers the Ark.Tools gRPC mediator infrastructure.</summary>
public static class ArkGrpcServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default gRPC user-context interceptor and registers its mediator provider.
    /// </summary>
    /// <param name="services">The ASP.NET Core service collection.</param>
    /// <param name="container">The Simple Injector container resolving mediator handlers.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddArkGrpc(this IServiceCollection services, Container container)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);

        services.AddCodeFirstGrpc(options => options.Interceptors.Add<ArkGrpcUserContextInterceptor>());
        container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, ArkGrpcUserContextProvider>();
        return services;
    }
}
