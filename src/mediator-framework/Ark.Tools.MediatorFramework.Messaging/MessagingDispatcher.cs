// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using SimpleInjector;
using SimpleInjector.Lifestyles;
using System.Collections.ObjectModel;
using System.Diagnostics;
using NodaTime;

using NLog;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Dispatches locked deliveries with explicit settlement and retry semantics.</summary>
public sealed class MessagingDispatcher
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Container _container;
    private readonly MessagingHeaderProcessor _headerProcessor;
    private readonly MessagingPayloadReceiver _payloadReceiver;
    private readonly IMessagingRetryPolicy _retryPolicy;
    private readonly Func<string, IMessagingPayloadReader, ICommandProcessor, CancellationToken, Task> _dispatch;
    private readonly Func<
        string,
        IMessagingPayloadReader,
        int,
        MessagingExceptionInfo,
        ICommandProcessor,
        CancellationToken,
        Task>? _dispatchFailed;
    private readonly IReadOnlyList<Type> _incomingStepTypes;
    private readonly Func<Type, object> _resolveStep;
    private readonly TimeSpan _lockRenewalInterval;
    private readonly IClock _clock;

    /// <summary>Creates a receive dispatcher for one participant.</summary>
    /// <param name="container">The participant's SimpleInjector container.</param>
    /// <param name="headerProcessor">The bounded header classifier.</param>
    /// <param name="payloadReceiver">The payload preparation runtime.</param>
    /// <param name="retryPolicy">The participant retry policy.</param>
    /// <param name="dispatch">The generated normal-message binder.</param>
    /// <param name="dispatchFailed">The generated inline failure binder, when installed.</param>
    /// <param name="incomingStepTypes">The incoming pipeline steps in execution order.</param>
    /// <param name="resolveStep">The pipeline step resolver.</param>
    /// <param name="lockRenewalInterval">The bounded interval between lock renewals.</param>
    public MessagingDispatcher(
        Container container,
        MessagingHeaderProcessor headerProcessor,
        MessagingPayloadReceiver payloadReceiver,
        IMessagingRetryPolicy retryPolicy,
        Func<string, IMessagingPayloadReader, ICommandProcessor, CancellationToken, Task> dispatch,
        Func<
            string,
            IMessagingPayloadReader,
            int,
            MessagingExceptionInfo,
            ICommandProcessor,
            CancellationToken,
            Task>? dispatchFailed = null,
        IReadOnlyList<Type>? incomingStepTypes = null,
        Func<Type, object>? resolveStep = null,
        TimeSpan? lockRenewalInterval = null,
        IClock? clock = null)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _headerProcessor = headerProcessor ?? throw new ArgumentNullException(nameof(headerProcessor));
        _payloadReceiver = payloadReceiver ?? throw new ArgumentNullException(nameof(payloadReceiver));
        MessagingRetryPolicyValidation.Validate(retryPolicy);
        _retryPolicy = retryPolicy;
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        _dispatchFailed = dispatchFailed;
        if (_retryPolicy.SecondLevelRetriesEnabled && _dispatchFailed is null)
            throw new ArgumentNullException(
                nameof(dispatchFailed),
                "Second-level retries require a generated failure binder.");
        _incomingStepTypes = new ReadOnlyCollection<Type>(
            (incomingStepTypes ?? Array.Empty<Type>()).ToArray());
        _resolveStep = resolveStep ?? container.GetInstance;
        _lockRenewalInterval = lockRenewalInterval ?? TimeSpan.FromSeconds(15);
        _clock = clock ?? SystemClock.Instance;
        if (_lockRenewalInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lockRenewalInterval));
    }

    /// <summary>Processes one locked delivery and applies exactly one settlement.</summary>
    /// <param name="delivery">The locked transport delivery.</param>
    /// <param name="cancellationToken">The host cancellation token.</param>
    /// <returns>A task that completes after processing and settlement.</returns>
    public async Task OnDeliveryAsync(
        IMessagingLockedDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var stopwatch = Stopwatch.StartNew();
        var outcome = "error";
        try
        {
            var (codec, logicalName) = _headerProcessor.Classify(delivery.Headers);
#pragma warning disable MA0004 // The reader is disposed after the delivery stage completes.
            await using var payload = await _payloadReceiver
                .PreparePayloadReaderAsync(delivery.Headers, delivery.Payload, codec, cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore MA0004
            var error = await _dispatchNormalAsync(
                delivery,
                logicalName,
                payload,
                cancellationToken).ConfigureAwait(false);
            var classification = error is null
                ? MessagingExceptionClassification.None
                : error.ExceptionType == typeof(MessagingFailFastException).FullName
                    ? MessagingExceptionClassification.FailFast
                    : MessagingExceptionClassification.Other;
            var decision = MessagingSettlement.Decide(
                delivery.DeliveryCount,
                _retryPolicy,
                classification,
                isSecondLevelStage: false);

            if (decision == MessagingSettlementDecision.RunSecondLevel)
            {
                var secondLevel = await _dispatchSecondLevelAsync(
                    delivery,
                    logicalName,
                    payload,
                    error!,
                    cancellationToken).ConfigureAwait(false);
                decision = secondLevel.Decision;
                error = secondLevel.Error ?? error;
            }

            await _settleAsync(delivery, decision, error, cancellationToken).ConfigureAwait(false);
            outcome = decision switch
            {
                MessagingSettlementDecision.Complete => "complete",
                MessagingSettlementDecision.Abandon => "abandon",
                MessagingSettlementDecision.DeadLetter => "dead_letter",
                _ => "error",
            };
            _logger.Debug(
                CultureInfo.InvariantCulture,
                "Messaging delivery {MessageType} at count {DeliveryCount} settled as {Settlement}",
                logicalName,
                delivery.DeliveryCount,
                decision);
        }
        catch (MessagingFailFastException exception)
        {
            outcome = "dead_letter";
            _logger.Warn(
                exception,
                CultureInfo.InvariantCulture,
                "Messaging delivery failed fast with {Reason}: {Description}",
                exception.Reason,
                exception.Message ?? string.Empty);
            await delivery.DeadLetterAsync(
                exception.Reason.ToString(),
                exception.Message ?? string.Empty,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            MessagingMetrics.RecordProcessing(
                stopwatch.Elapsed,
                delivery.Headers,
                outcome,
                delivery.DeliveryCount,
                now: _clock.GetCurrentInstant().ToDateTimeOffset());
        }
    }

    private async Task<MessagingExceptionInfo?> _dispatchNormalAsync(
        IMessagingLockedDelivery delivery,
        string logicalName,
        IMessagingPayloadReader payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await _invokeStageAsync(
                delivery,
                async stageToken =>
                {
#pragma warning disable MA0004 // The scope lifetime is bounded by this delivery stage.
                    await using var scope = AsyncScopedLifestyle.BeginScope(_container);
#pragma warning restore MA0004
                    var context = new MessagingIncomingContext(
                        delivery.Headers,
                        payload.ReadPayload(),
                        delivery.DeliveryCount,
                        stageToken);
                    context.Items[MessagingMetrics.DispatcherManagedItem] = true;
                    var processor = scope.GetInstance<ICommandProcessor>();
                    await MessagingPipelineInvoker.InvokeIncomingAsync(
                        _incomingStepTypes,
                        _resolveStep,
                        context,
                        () => _dispatch(logicalName, payload, processor, stageToken),
                        stageToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (MessagingFailFastException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = MessagingExceptionInfo.From(exception);
            _logger.Warn(
                exception,
                CultureInfo.InvariantCulture,
                "Messaging delivery failed with {ExceptionType}: {Message}",
                error.ExceptionType,
                error.Message);
            return error;
        }
    }

    private async Task<(MessagingSettlementDecision Decision, MessagingExceptionInfo? Error)> _dispatchSecondLevelAsync(
        IMessagingLockedDelivery delivery,
        string logicalName,
        IMessagingPayloadReader payload,
        MessagingExceptionInfo error,
        CancellationToken cancellationToken)
    {
        if (_dispatchFailed is null)
            return (MessagingSettlementDecision.DeadLetter, null);

        try
        {
            await _invokeStageAsync(
                delivery,
                async stageToken =>
                {
#pragma warning disable MA0004 // The scope lifetime is bounded by this delivery stage.
                    await using var scope = AsyncScopedLifestyle.BeginScope(_container);
#pragma warning restore MA0004
                    var processor = scope.GetInstance<ICommandProcessor>();
                    await _dispatchFailed(
                        logicalName,
                        payload,
                        delivery.DeliveryCount,
                        error,
                        processor,
                        stageToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return (MessagingSettlementDecision.Complete, null);
        }
        catch (ActivationException exception)
        {
            var failFast = new MessagingFailFastException(
                MessagingFailFastReason.MissingSecondLevelHandler,
                logicalName,
                exception);
            _logger.Warn(
                exception,
                CultureInfo.InvariantCulture,
                "Messaging second-level handler activation failed for {MessageType}",
                logicalName);
            return (MessagingSettlementDecision.DeadLetter, MessagingExceptionInfo.From(failFast));
        }
        catch (MessagingFailFastException exception)
        {
            _logger.Warn(
                exception,
                CultureInfo.InvariantCulture,
                "Messaging second-level delivery failed fast with {Reason}: {Description}",
                exception.Reason,
                exception.Message ?? string.Empty);
            return (MessagingSettlementDecision.DeadLetter, MessagingExceptionInfo.From(exception));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable ERP022 // Second-level failures intentionally map to normal retry.
        catch (Exception exception)
        {
            var failureInfo = MessagingExceptionInfo.From(exception);
            _logger.Warn(
                exception,
                CultureInfo.InvariantCulture,
                "Messaging second-level delivery failed with {ExceptionType}: {Message}",
                failureInfo.ExceptionType,
                failureInfo.Message);
            return (MessagingSettlementDecision.Abandon, null);
        }
#pragma warning restore ERP022
    }

    private async Task _invokeStageAsync(
        IMessagingLockedDelivery delivery,
        Func<CancellationToken, Task> stage,
        CancellationToken cancellationToken)
    {
        using var stageCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stageCancellation.CancelAfter(_retryPolicy.MaximumHandlerDuration);
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            stageCancellation.Token);
        var renewal = _renewLockAsync(delivery, renewalCancellation.Token);
        Exception? renewalFailure = null;
        try
        {
            await stage(stageCancellation.Token).ConfigureAwait(false);
            stageCancellation.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            await renewalCancellation.CancelAsync().ConfigureAwait(false);
            try
            {
                await renewal.ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                if (!renewalCancellation.IsCancellationRequested)
                    renewalFailure = exception;
            }
            catch (Exception exception)
            {
                renewalFailure = exception;
            }
        }

        if (renewalFailure is not null)
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(renewalFailure).Throw();
    }

    private async Task _renewLockAsync(
        IMessagingLockedDelivery delivery,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(_lockRenewalInterval, cancellationToken).ConfigureAwait(false);
            await delivery.RenewLockAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task _settleAsync(
        IMessagingLockedDelivery delivery,
        MessagingSettlementDecision decision,
        MessagingExceptionInfo? error,
        CancellationToken cancellationToken)
    {
        switch (decision)
        {
            case MessagingSettlementDecision.Complete:
                await delivery.CompleteAsync(cancellationToken).ConfigureAwait(false);
                break;
            case MessagingSettlementDecision.Abandon:
                await delivery.AbandonAsync(cancellationToken).ConfigureAwait(false);
                break;
            case MessagingSettlementDecision.DeadLetter:
                await delivery.DeadLetterAsync(
                    error?.ExceptionType ?? "fail-fast",
                    error?.Message ?? string.Empty,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("Second-level dispatch must be resolved before settlement.");
        }
    }
}
