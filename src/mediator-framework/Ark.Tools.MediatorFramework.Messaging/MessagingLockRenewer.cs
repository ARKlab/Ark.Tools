// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NLog;

using NodaTime;

using System.Buffers;
using System.Collections.Concurrent;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>A delivery whose lock is kept alive by the host-scoped renewer.</summary>
internal interface IMessagingRenewedDelivery : IMessagingLockedDelivery
{
    /// <summary>Gets a token cancelled when the lock is lost and the handler must stop.</summary>
    CancellationToken _lockLost { get; }
}

/// <summary>Keeps the locks of buffered and in-flight deliveries alive from a single timer.</summary>
/// <remarks>
/// One renewer per host: cost is O(1) timers rather than one timer per in-flight message. A delivery
/// is registered when it enters the buffer, not when a worker picks it up, which is what makes
/// prefetch lock-safe. The renewer owns the authoritative lock state per delivery and serialises
/// renewal against settlement, so a renewal can never race the settle call that consumes the lock.
/// </remarks>
internal sealed class MessagingLockRenewer : IAsyncDisposable
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly MessagingProcessingOptions _options;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<RenewedDelivery, byte> _registrations = new();

    /// <summary>Creates a renewer for one processor host.</summary>
    /// <param name="options">The processing options carrying the renewal cadence.</param>
    /// <param name="clock">The clock, or <see langword="null"/> for the system clock.</param>
    internal MessagingLockRenewer(MessagingProcessingOptions options, IClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _clock = clock ?? SystemClock.Instance;
    }

    /// <summary>Gets the number of deliveries whose lock is currently being kept alive.</summary>
    internal int _count => _registrations.Count;

    /// <summary>Registers a delivery entering the buffer and returns the renewal-aware wrapper.</summary>
    /// <param name="delivery">The freshly received delivery.</param>
    /// <returns>The delivery to hand to a worker; settling it deregisters it.</returns>
    internal IMessagingLockedDelivery _register(IMessagingLockedDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var registration = new RenewedDelivery(this, delivery, _clock.GetCurrentInstant());
        _registrations[registration] = 0;
        return registration;
    }

    /// <summary>Runs the single renewal timer until cancelled.</summary>
    /// <param name="ctk">The host cancellation token.</param>
    /// <returns>A task that completes when the host stops.</returns>
    internal async Task _runAsync(CancellationToken ctk)
    {
        while (!ctk.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.RenewalScanInterval, ctk).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await _tickAsync(ctk).ConfigureAwait(false);
        }
    }

    /// <summary>Renews every delivery that is due, bounded so one tick cannot stall the timer.</summary>
    /// <param name="ctk">The host cancellation token.</param>
    /// <returns>A task that completes when the due batch has been renewed.</returns>
    internal async Task _tickAsync(CancellationToken ctk)
    {
        var now = _clock.GetCurrentInstant();
        List<Task>? due = null;
        foreach (var registration in _registrations.Keys)
        {
            if (!registration._isDue(now, _options.RenewalSafetyMargin))
                continue;

            due ??= new List<Task>(_options.MaximumRenewalBatch);
            due.Add(registration._renewAsync(ctk));
            if (due.Count >= _options.MaximumRenewalBatch)
                break;
        }

        if (due is not null)
            await Task.WhenAll(due).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var registration in _registrations.Keys)
            await registration.DisposeAsync().ConfigureAwait(false);

        _registrations.Clear();
    }

    private void _deregister(RenewedDelivery registration)
    {
        _registrations.TryRemove(registration, out _);
    }

    /// <summary>Wraps a delivery so settlement deregisters it and can never race a renewal.</summary>
    private sealed class RenewedDelivery : IMessagingRenewedDelivery, IAsyncDisposable
    {
        private readonly MessagingLockRenewer _renewer;
        private readonly IMessagingLockedDelivery _inner;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly CancellationTokenSource _lockLostSource = new();
        private Instant _acquiredAt;
        private bool _settled;

        internal RenewedDelivery(
            MessagingLockRenewer renewer,
            IMessagingLockedDelivery inner,
            Instant acquiredAt)
        {
            _renewer = renewer;
            _inner = inner;
            _acquiredAt = acquiredAt;
        }

        public CancellationToken _lockLost => _lockLostSource.Token;

        public IReadOnlyDictionary<string, string> Headers => _inner.Headers;

        public ReadOnlySequence<byte> Payload => _inner.Payload;

        public int DeliveryCount => _inner.DeliveryCount;

        public string DeliveryId => _inner.DeliveryId;

        public DateTimeOffset? LockedUntil => _inner.LockedUntil;

        public async Task RenewLockAsync(CancellationToken ctk)
        {
            await _renewAsync(ctk).ConfigureAwait(false);
        }

        public async Task CompleteAsync(CancellationToken ctk)
        {
            await _settleAsync(_inner.CompleteAsync, ctk).ConfigureAwait(false);
        }

        public async Task AbandonAsync(CancellationToken ctk)
        {
            await _settleAsync(_inner.AbandonAsync, ctk).ConfigureAwait(false);
        }

        public async Task DeadLetterAsync(string reason, string description, CancellationToken ctk)
        {
            await _settleAsync(
                token => _inner.DeadLetterAsync(reason, description, token),
                ctk).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                _settled = true;
            }
            finally
            {
                _gate.Release();
            }

            _lockLostSource.Dispose();
            _gate.Dispose();
        }

        /// <summary>Decides whether the lock is close enough to expiry to renew now.</summary>
        internal bool _isDue(Instant now, TimeSpan safetyMargin)
        {
            if (_settled || _inner.LockedUntil is not { } lockedUntil)
                return false;

            var expiry = Instant.FromDateTimeOffset(lockedUntil);
            var half = (expiry - _acquiredAt) / 2;
            var margin = half > Duration.FromTimeSpan(safetyMargin)
                ? half
                : Duration.FromTimeSpan(safetyMargin);
            return now >= expiry - margin;
        }

        internal async Task _renewAsync(CancellationToken ctk)
        {
            if (!await _gate.WaitAsync(0, ctk).ConfigureAwait(false))
                return;

            try
            {
                // Settlement consumes the lock, so a renewal that lost the race must not run.
                if (_settled)
                    return;

                await _inner.RenewLockAsync(ctk).ConfigureAwait(false);
                _acquiredAt = _renewer._clock.GetCurrentInstant();
            }
            catch (OperationCanceledException) when (ctk.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // Any renewal failure means the lock is gone.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                // ponytail: the failure cancels the handler and drops the registration. Ceiling: the
                // lock-renewal instrument and the concurrency-controller feed land in AMF-05/AMF-09.
                _logger.Warn(
                    exception,
                    CultureInfo.InvariantCulture,
                    "Lock renewal failed for delivery {deliveryId}; cancelling its handler.",
                    _inner.DeliveryId);
                _settled = true;
                _renewer._deregister(this);
                await _lockLostSource.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task _settleAsync(Func<CancellationToken, Task> settle, CancellationToken ctk)
        {
            await _gate.WaitAsync(ctk).ConfigureAwait(false);
            try
            {
                _settled = true;
                _renewer._deregister(this);
                await settle(ctk).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
