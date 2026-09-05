// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SimpleInjector;

using System.Diagnostics;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Immutable Server-Sent Events behavior resolved from <c>[Sse]</c> at generation time.</summary>
public sealed class ArkSseSettings
{
    /// <summary>Initializes a new instance of the <see cref="ArkSseSettings"/> class.</summary>
    /// <param name="interval">The server-side poll interval, or <see cref="TimeSpan.Zero"/> for a streaming query.</param>
    /// <param name="minimumInterval">The smallest interval a client may request.</param>
    /// <param name="maximumInterval">The largest interval a client may request.</param>
    /// <param name="allowClientInterval">Whether the client may request an interval.</param>
    /// <param name="heartbeat">The idle interval after which a heartbeat event is emitted.</param>
    /// <param name="maxConnection">The maximum connection lifetime, or <see cref="TimeSpan.Zero"/> for no cap.</param>
    /// <param name="emitEveryTick">Whether every poll emits an event.</param>
    /// <param name="eventName">The SSE event name used for data frames.</param>
    public ArkSseSettings(
        TimeSpan interval,
        TimeSpan minimumInterval,
        TimeSpan maximumInterval,
        bool allowClientInterval,
        TimeSpan heartbeat,
        TimeSpan maxConnection,
        bool emitEveryTick,
        string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minimumInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumInterval, minimumInterval);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heartbeat, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConnection, TimeSpan.Zero);

        Interval = interval;
        MinimumInterval = minimumInterval;
        MaximumInterval = maximumInterval;
        AllowClientInterval = allowClientInterval;
        Heartbeat = heartbeat;
        MaxConnection = maxConnection;
        EmitEveryTick = emitEveryTick;
        EventName = eventName;
    }

    /// <summary>Gets the server-side poll interval.</summary>
    public TimeSpan Interval { get; }

    /// <summary>Gets the smallest interval a client may request.</summary>
    public TimeSpan MinimumInterval { get; }

    /// <summary>Gets the largest interval a client may request.</summary>
    public TimeSpan MaximumInterval { get; }

    /// <summary>Gets whether the client may request an interval.</summary>
    public bool AllowClientInterval { get; }

    /// <summary>Gets the idle interval after which a heartbeat event is emitted.</summary>
    public TimeSpan Heartbeat { get; }

    /// <summary>Gets the maximum connection lifetime, or <see cref="TimeSpan.Zero"/> for no cap.</summary>
    public TimeSpan MaxConnection { get; }

    /// <summary>Gets whether every poll emits an event.</summary>
    public bool EmitEveryTick { get; }

    /// <summary>Gets the SSE event name used for data frames.</summary>
    public string EventName { get; }
}

/// <summary>
/// Caps the number of concurrent Server-Sent Events connections. Register a configured instance as a
/// singleton to override the defaults; generated endpoints fall back to a process-wide default.
/// </summary>
public sealed class ArkSseConnectionTracker
{
    // ponytail: the counters are per-process, so behind a load balancer the effective cap is
    // (instances * MaxConcurrentConnections). Upgrade path when a global cap is required: back
    // TryAcquire with a distributed counter (for example Redis) keyed by principal.
    private static readonly ArkSseConnectionTracker _default = new();
    private readonly Dictionary<string, int> _perPrincipal = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private int _total;

    /// <summary>Initializes a new instance of the <see cref="ArkSseConnectionTracker"/> class.</summary>
    /// <param name="maxConcurrentConnections">The maximum number of concurrent connections per process.</param>
    /// <param name="maxConcurrentConnectionsPerPrincipal">The maximum number of concurrent connections per principal.</param>
    public ArkSseConnectionTracker(int maxConcurrentConnections = 1000, int maxConcurrentConnectionsPerPrincipal = 5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrentConnections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrentConnectionsPerPrincipal);

        MaxConcurrentConnections = maxConcurrentConnections;
        MaxConcurrentConnectionsPerPrincipal = maxConcurrentConnectionsPerPrincipal;
    }

    /// <summary>Gets the maximum number of concurrent connections per process.</summary>
    public int MaxConcurrentConnections { get; }

    /// <summary>Gets the maximum number of concurrent connections per principal.</summary>
    public int MaxConcurrentConnectionsPerPrincipal { get; }

    /// <summary>Gets the number of connections currently held.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
                return _total;
        }
    }

    /// <summary>Gets the tracker registered in the container, or the process-wide default.</summary>
    /// <param name="services">The request service provider.</param>
    /// <returns>The tracker that governs this connection.</returns>
    public static ArkSseConnectionTracker Resolve(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetService<ArkSseConnectionTracker>() ?? _default;
    }

    /// <summary>Attempts to reserve a connection slot.</summary>
    /// <param name="principal">The principal name, or <see langword="null"/> for an anonymous caller.</param>
    /// <returns>A lease that must be disposed, or <see langword="null"/> when a cap was reached.</returns>
    public IDisposable? TryAcquire(string? principal)
    {
        var key = string.IsNullOrEmpty(principal) ? "\u0000anonymous" : principal;
        lock (_gate)
        {
            if (_total >= MaxConcurrentConnections)
                return null;

            _perPrincipal.TryGetValue(key, out var current);
            if (current >= MaxConcurrentConnectionsPerPrincipal)
                return null;

            _total++;
            _perPrincipal[key] = current + 1;
        }

        return new Lease(this, key);
    }

    private void _release(string key)
    {
        lock (_gate)
        {
            _total--;
            if (_perPrincipal.TryGetValue(key, out var current) && current <= 1)
                _perPrincipal.Remove(key);
            else
                _perPrincipal[key] = current - 1;
        }
    }

    private sealed class Lease : IDisposable
    {
        private readonly ArkSseConnectionTracker _tracker;
        private readonly string _key;
        private bool _released;

        public Lease(ArkSseConnectionTracker tracker, string key)
        {
            _tracker = tracker;
            _key = key;
        }

        public void Dispose()
        {
            if (_released)
                return;

            _released = true;
            _tracker._release(_key);
        }
    }
}

/// <summary>Helpers used by generated Server-Sent Events endpoints.</summary>
public static class ArkSse
{
    /// <summary>The SSE event name used for idle heartbeat frames, which carry no meaningful payload.</summary>
    public const string HeartbeatEventName = "heartbeat";

    /// <summary>The query-string parameter that requests a client-side poll interval, in seconds.</summary>
    public const string IntervalParameterName = "pollIntervalSeconds";

    /// <summary>
    /// Re-executes a query on an interval and frames each changed result as an event. Authorization,
    /// validation and every other query decorator run again on each poll.
    /// </summary>
    /// <typeparam name="TQuery">The query contract type.</typeparam>
    /// <typeparam name="TResponse">The query result type.</typeparam>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="query">The bound query.</param>
    /// <param name="settings">The SSE behavior declared by the contract.</param>
    /// <param name="changeToken">
    /// Selects the response ETag used both as the event id and as the change token, or
    /// <see langword="null"/> to compare the serialized payloads instead.
    /// </param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The SSE response, or <c>503 Service Unavailable</c> when a connection cap was reached.</returns>
    [SuppressMessage("Reliability", "CA2000", Justification = "The connection lease is owned and disposed by the returned event stream.")]
    public static IResult Poll<TQuery, TResponse>(
        HttpContext httpContext,
        TQuery query,
        ArkSseSettings settings,
        Func<TResponse, string?>? changeToken,
        CancellationToken cancellationToken)
        where TQuery : class, IQuery<TQuery, TResponse>
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(settings);

        var lease = _prepare(httpContext, settings);
        if (lease is null)
            return _unavailable(httpContext, settings);

        return TypedResults.ServerSentEvents(
            _pollAsync(httpContext, query, settings, changeToken, lease, cancellationToken));
    }

    /// <summary>Frames the items of a streaming query response as events.</summary>
    /// <typeparam name="TItem">The streamed item type.</typeparam>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="settings">The SSE behavior declared by the contract.</param>
    /// <param name="source">The handler response sequence.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The SSE response, or <c>503 Service Unavailable</c> when a connection cap was reached.</returns>
    [SuppressMessage("Reliability", "CA2000", Justification = "The connection lease is owned and disposed by the returned event stream.")]
    public static IResult Stream<TItem>(
        HttpContext httpContext,
        ArkSseSettings settings,
        IAsyncEnumerable<TItem> source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(source);

        var lease = _prepare(httpContext, settings);
        if (lease is null)
            return _unavailable(httpContext, settings);

        return TypedResults.ServerSentEvents(
            _streamAsync(httpContext, settings, source, lease, cancellationToken));
    }

    /// <summary>Resolves the poll interval requested by the client, clamped to the declared bounds.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="settings">The SSE behavior declared by the contract.</param>
    /// <returns>The interval to use for this connection, always within the declared bounds.</returns>
    public static TimeSpan ResolveInterval(HttpContext httpContext, ArkSseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(settings);

        var requested = settings.Interval;
        if (settings.AllowClientInterval
            && httpContext.Request.Query.TryGetValue(IntervalParameterName, out var raw)
            && int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            requested = TimeSpan.FromSeconds(seconds);

        if (requested < settings.MinimumInterval)
            return settings.MinimumInterval;

        return requested > settings.MaximumInterval ? settings.MaximumInterval : requested;
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "The connection lease ownership is transferred to the caller.")]
    private static IDisposable? _prepare(HttpContext httpContext, ArkSseSettings settings)
    {
        var lease = ArkSseConnectionTracker.Resolve(httpContext.RequestServices)
            .TryAcquire(httpContext.User?.Identity?.Name);
        if (lease is null)
            return null;

        // Proxies and compression middleware must not buffer an open-ended response.
        httpContext.Response.Headers.CacheControl = "no-cache, no-store";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        return lease;
    }

    private static IResult _unavailable(HttpContext httpContext, ArkSseSettings settings)
    {
        var retryAfter = ResolveInterval(httpContext, settings);
        httpContext.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "SSE_CONNECTION_LIMIT",
            detail: "The server reached its Server-Sent Events connection limit. Retry later.");
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0050", Justification = "The iterator validates its arguments when enumeration begins.")]
    private static async IAsyncEnumerable<SseItem<TResponse>> _pollAsync<TQuery, TResponse>(
        HttpContext httpContext,
        TQuery query,
        ArkSseSettings settings,
        Func<TResponse, string?>? changeToken,
        IDisposable lease,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TQuery : class, IQuery<TQuery, TResponse>
    {
        using var scope = lease;
        using var cancellation = await _connectionCancellation(httpContext, settings, cancellationToken)
            .ConfigureAwait(false);
        var processor = httpContext.RequestServices
            .GetRequiredService<Container>()
            .GetInstance<IQueryProcessor>();
        var interval = ResolveInterval(httpContext, settings);
        // PeriodicTimer keeps at most one pending tick, so a slow client skips polls instead of
        // queueing them: there is no backpressure on an SSE connection.
        using var pollTimer = new PeriodicTimer(interval);
        using var heartbeatTimer = new PeriodicTimer(settings.Heartbeat);
        var lastToken = _lastEventId(httpContext);
        byte[]? lastPayload = null;
        var reconnection = interval;
        var emitted = false;
        var lastEmit = Stopwatch.GetTimestamp();
        Task<TResponse>? pendingQuery = processor.ExecuteAsync<TQuery, TResponse>(query, cancellation.Token);
        Task<bool>? pendingPoll = null;
        var pendingHeartbeat = heartbeatTimer.WaitForNextTickAsync(cancellation.Token).AsTask();

        while (true)
        {
            var completed = pendingQuery is null
                ? await Task.WhenAny(pendingPoll!, pendingHeartbeat).ConfigureAwait(false)
                : await Task.WhenAny(pendingQuery, pendingHeartbeat).ConfigureAwait(false);

            if (completed == pendingHeartbeat)
            {
                bool heartbeat;
                try
                {
                    heartbeat = await pendingHeartbeat.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

                if (!heartbeat)
                    yield break;

                if (Stopwatch.GetElapsedTime(lastEmit) >= settings.Heartbeat)
                {
                    yield return new SseItem<TResponse>(default!, HeartbeatEventName);
                    emitted = true;
                    lastEmit = Stopwatch.GetTimestamp();
                }

                pendingHeartbeat = heartbeatTimer.WaitForNextTickAsync(cancellation.Token).AsTask();
                continue;
            }

            if (pendingQuery is not null)
            {
                TResponse result;
                try
                {
                    result = await pendingQuery.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

                pendingQuery = null;
                pendingPoll = pollTimer.WaitForNextTickAsync(cancellation.Token).AsTask();

                var token = changeToken?.Invoke(result);
                var payload = changeToken is null && !settings.EmitEveryTick
                    ? _serialize(httpContext, result)
                    : null;
                var changed = settings.EmitEveryTick
                    || (changeToken is null
                        ? lastPayload is null || !lastPayload.AsSpan().SequenceEqual(payload)
                        : !string.Equals(token, lastToken, StringComparison.Ordinal));

                if (changed)
                {
                    lastToken = token;
                    lastPayload = payload;
                    yield return new SseItem<TResponse>(result, settings.EventName)
                    {
                        EventId = token,
                        ReconnectionInterval = emitted ? null : reconnection,
                    };
                    emitted = true;
                    lastEmit = Stopwatch.GetTimestamp();
                }

                continue;
            }

            bool next;
            try
            {
                next = await pendingPoll!.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (!next)
                yield break;

            pendingPoll = null;
            pendingQuery = processor.ExecuteAsync<TQuery, TResponse>(query, cancellation.Token);
        }
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0050", Justification = "The iterator validates its arguments when enumeration begins.")]
    private static async IAsyncEnumerable<SseItem<TItem>> _streamAsync<TItem>(
        HttpContext httpContext,
        ArkSseSettings settings,
        IAsyncEnumerable<TItem> source,
        IDisposable lease,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var scope = lease;
        using var cancellation = await _connectionCancellation(httpContext, settings, cancellationToken)
            .ConfigureAwait(false);
        await foreach (var item in source.WithCancellation(cancellation.Token).ConfigureAwait(false))
            yield return new SseItem<TItem>(item, settings.EventName);
    }

    private static async Task<CancellationTokenSource> _connectionCancellation(
        HttpContext httpContext,
        ArkSseSettings settings,
        CancellationToken cancellationToken)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            httpContext.RequestAborted);
        var lifetime = await _connectionLifetime(httpContext, settings).ConfigureAwait(false);
        if (lifetime is { } deadline)
            cancellation.CancelAfter(deadline);
        return cancellation;
    }

    /// <summary>
    /// Returns the remaining connection lifetime: the smaller of the declared cap and the time left on
    /// the caller's bearer token, because the principal is captured once and never re-authenticated.
    /// </summary>
    private static async Task<TimeSpan?> _connectionLifetime(HttpContext httpContext, ArkSseSettings settings)
    {
        TimeSpan? lifetime = settings.MaxConnection > TimeSpan.Zero ? settings.MaxConnection : null;
        DateTimeOffset? expiration = null;
        var authentication = httpContext.RequestServices.GetService<IAuthenticationService>();
        if (authentication is not null)
        {
            var result = await authentication.AuthenticateAsync(httpContext, scheme: null).ConfigureAwait(false);
            expiration = result.Properties?.ExpiresUtc;
        }

        if (expiration is null)
        {
            var expirationClaim = httpContext.User?.FindFirst("exp")?.Value;
            if (expirationClaim is not null
                && long.TryParse(expirationClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
                expiration = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        if (expiration is { } expiresUtc)
        {
            var remaining = expiresUtc - DateTimeOffset.UtcNow;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
            if (lifetime is null || remaining < lifetime)
                lifetime = remaining;
        }

        return lifetime;
    }

    private static string? _lastEventId(HttpContext httpContext)
    {
        var value = httpContext.Request.Headers["Last-Event-ID"].ToString();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static byte[] _serialize<TResponse>(HttpContext httpContext, TResponse value)
    {
        var options = httpContext.RequestServices
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;
        var typeInfo = options.GetTypeInfo(typeof(TResponse));
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }
}
