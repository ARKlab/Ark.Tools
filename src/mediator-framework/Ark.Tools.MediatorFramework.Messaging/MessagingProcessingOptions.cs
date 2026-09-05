// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Host-side options for a messaging processor.</summary>
/// <remarks>
/// These options shape the processing side only; nothing here affects sending. Defaults target a
/// balanced processor host, and <see cref="InitialConcurrency"/> set to one restores strictly
/// sequential processing.
/// </remarks>
public sealed class MessagingProcessingOptions
{
    private int _initialConcurrency = Environment.ProcessorCount;
    private int _minimumConcurrency = 1;
    private int _maximumConcurrency = Environment.ProcessorCount * 8;
    private int _receiveChannels = 1;
    private double _prefetchMultiplier = 2;
    private double _lockSafetyFactor = 0.5;
    private TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the initial number of concurrent workers. Defaults to the processor count.</summary>
    public int InitialConcurrency
    {
        get => _initialConcurrency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _initialConcurrency = value;
        }
    }

    /// <summary>Gets or sets the lower bound for the concurrency limit. Defaults to one.</summary>
    public int MinimumConcurrency
    {
        get => _minimumConcurrency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minimumConcurrency = value;
        }
    }

    /// <summary>Gets or sets the upper bound for the concurrency limit. Defaults to eight times the processor count.</summary>
    public int MaximumConcurrency
    {
        get => _maximumConcurrency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maximumConcurrency = value;
        }
    }

    /// <summary>Gets or sets the number of overlapping receive loops. Defaults to one.</summary>
    public int ReceiveChannels
    {
        get => _receiveChannels;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _receiveChannels = value;
        }
    }

    /// <summary>Gets or sets the prefetch budget multiplier applied to the concurrency limit. Defaults to two.</summary>
    public double PrefetchMultiplier
    {
        get => _prefetchMultiplier;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _prefetchMultiplier = value;
        }
    }

    /// <summary>Gets or sets the fraction of the native lock duration a full buffer may take to drain. Defaults to one half.</summary>
    public double LockSafetyFactor
    {
        get => _lockSafetyFactor;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1);
            _lockSafetyFactor = value;
        }
    }

    /// <summary>Gets or sets how long a graceful stop drains in-flight work. Defaults to thirty seconds.</summary>
    public TimeSpan ShutdownTimeout
    {
        get => _shutdownTimeout;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _shutdownTimeout = value;
        }
    }

    /// <summary>Validates the option combination and throws a named diagnostic when impossible.</summary>
    /// <exception cref="MessagingCompositionException">The options cannot be satisfied.</exception>
    public void Validate()
    {
        if (MinimumConcurrency > MaximumConcurrency)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
                FormattableString.Invariant(
                    $"MinimumConcurrency ({MinimumConcurrency}) cannot exceed MaximumConcurrency ({MaximumConcurrency})."));
        }

        if (InitialConcurrency < MinimumConcurrency || InitialConcurrency > MaximumConcurrency)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
                FormattableString.Invariant(
                    $"InitialConcurrency ({InitialConcurrency}) must be between MinimumConcurrency ({MinimumConcurrency}) and MaximumConcurrency ({MaximumConcurrency})."));
        }
    }
}
