// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>
/// Exposes a query that already declares <see cref="HttpEndpointAttribute"/> with <c>GET</c> as an
/// additional Server-Sent Events route. The route, versioning, authorization and OpenAPI metadata
/// are inherited from the declared HTTP endpoint; only the SSE-specific behavior is configured here.
/// </summary>
/// <remarks>
/// The behavior depends on the query result shape:
/// a query returning <c>Task&lt;T&gt;</c> is re-executed every <see cref="IntervalSeconds"/> and each
/// result is framed as an event; a query returning <c>IAsyncEnumerable&lt;T&gt;</c> is enumerated once
/// and each yielded item is framed as an event, in which case <see cref="IntervalSeconds"/> must be zero.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SseAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the server-side polling interval in seconds. Required for polled queries and
    /// rejected for streaming queries.
    /// </summary>
    public int IntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the smallest interval a client may request. The default is 60 seconds, because a
    /// poller multiplies its cost by the number of connected clients.
    /// </summary>
    public int MinimumIntervalSeconds { get; set; } = 60;

    /// <summary>Gets or sets the largest interval a client may request. The default is one hour.</summary>
    public int MaximumIntervalSeconds { get; set; } = 3600;

    /// <summary>
    /// Gets or sets whether the client may override the interval through the
    /// <c>pollIntervalSeconds</c> query-string parameter. The requested value is always clamped
    /// server-side to <see cref="MinimumIntervalSeconds"/>..<see cref="MaximumIntervalSeconds"/>.
    /// </summary>
    public bool AllowClientInterval { get; set; }

    /// <summary>
    /// Gets or sets the idle interval, in seconds, after which a heartbeat event is emitted so that
    /// proxies and load balancers do not close an idle connection. The default is 15 seconds.
    /// </summary>
    public int HeartbeatSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets the maximum connection lifetime in seconds, after which the server completes the
    /// response and the client reconnects. The default is one hour. Zero disables the cap, which also
    /// removes the reconnection that re-evaluates an expired bearer token.
    /// </summary>
    public int MaxConnectionSeconds { get; set; } = 3600;

    /// <summary>
    /// Gets or sets whether every poll emits an event. The default is <see langword="false"/>, which
    /// emits an event only when the result changed since the previously emitted one.
    /// </summary>
    public bool EmitEveryTick { get; set; }

    /// <summary>
    /// Gets or sets the route suffix appended to the declared HTTP endpoint template. The default
    /// names the behavior rather than the transport: <c>/poller</c> for a polled query and
    /// <c>/stream</c> for a streaming one.
    /// </summary>
    public string? RouteSuffix { get; set; }

    /// <summary>
    /// Gets or sets the SSE <c>event</c> name used for data frames. The default is the contract name.
    /// </summary>
    public string? EventName { get; set; }
}
