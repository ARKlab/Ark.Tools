// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Hosting;

using NLog;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Represents effective Azure Functions Queue Storage host settings.</summary>
public sealed class StorageQueueFunctionsHostSettings
{
    /// <summary>Creates effective Queue Storage host settings.</summary>
    /// <param name="messageEncoding">The effective queues message encoding.</param>
    /// <param name="maximumDequeueCount">The effective maximum dequeue count.</param>
    /// <param name="visibilityTimeout">The effective retry visibility timeout.</param>
    public StorageQueueFunctionsHostSettings(
        string messageEncoding,
        int maximumDequeueCount,
        TimeSpan visibilityTimeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageEncoding);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDequeueCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(
            visibilityTimeout.Ticks,
            nameof(visibilityTimeout));

        MessageEncoding = messageEncoding;
        MaximumDequeueCount = maximumDequeueCount;
        VisibilityTimeout = visibilityTimeout;
    }

    /// <summary>Gets the effective queues message encoding.</summary>
    public string MessageEncoding { get; }

    /// <summary>Gets the effective maximum dequeue count.</summary>
    public int MaximumDequeueCount { get; }

    /// <summary>Gets the effective retry visibility timeout.</summary>
    public TimeSpan VisibilityTimeout { get; }
}

/// <summary>Validates effective Storage Queue host settings when a Functions host starts.</summary>
public sealed class StorageQueueFunctionsHostSettingsValidator : IHostedService
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly MessagingFunctionsManifest _manifest;
    private readonly StorageQueueFunctionsHostSettings _settings;

    /// <summary>Creates the startup validator.</summary>
    /// <param name="manifest">The generated messaging host manifest.</param>
    /// <param name="settings">The effective Queue Storage host settings.</param>
    public StorageQueueFunctionsHostSettingsValidator(
        MessagingFunctionsManifest manifest,
        StorageQueueFunctionsHostSettings settings)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_manifest.TriggerBinding != MessagingFunctionsTriggerBinding.StorageQueue)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return;
        }
        if (_manifest.RetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException(
                "Storage Queue messaging participants require a positive RetryDelay.");

        var encodingMatches = string.Equals(
            _settings.MessageEncoding,
            "none",
            StringComparison.Ordinal);
        var dequeueMatches =
            _settings.MaximumDequeueCount == _manifest.MaximumDeliveryCount;
        var visibilityMatches = _settings.VisibilityTimeout == _manifest.RetryDelay;
        if (encodingMatches && dequeueMatches && visibilityMatches)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return;
        }

        if (_manifest.StrictStorageQueueHostSettings)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Storage Queue host settings do not match the generated manifest. "
                    + "Expected messageEncoding=none, maxDequeueCount={0}, visibilityTimeout={1}; "
                    + "actual messageEncoding={2}, maxDequeueCount={3}, visibilityTimeout={4}.",
                    _manifest.MaximumDeliveryCount,
                    _manifest.RetryDelay,
                    _settings.MessageEncoding,
                    _settings.MaximumDequeueCount,
                    _settings.VisibilityTimeout));
        }

        _logger.Warn(
            CultureInfo.InvariantCulture,
            "Storage Queue host settings differ from the generated manifest. "
            + "Expected messageEncoding={ExpectedMessageEncoding}, "
            + "maxDequeueCount={ExpectedMaxDequeueCount}, visibilityTimeout={ExpectedVisibilityTimeout}; "
            + "actual messageEncoding={ActualMessageEncoding}, "
            + "maxDequeueCount={ActualMaxDequeueCount}, visibilityTimeout={ActualVisibilityTimeout}",
            "none",
            _manifest.MaximumDeliveryCount,
            _manifest.RetryDelay,
            _settings.MessageEncoding,
            _settings.MaximumDequeueCount,
            _settings.VisibilityTimeout);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
