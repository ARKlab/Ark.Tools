﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Retry;
using Rebus.Retry.FailFast;
using Rebus.Retry.Simple;
using Rebus.Transport;

namespace Ark.Tools.Rebus.Retry
{

    /// <summary>
    /// Incoming message pipeline step that implements a retry mechanism - if the call to the rest of the pipeline fails,
    /// the exception is caught and the queue transaction is rolled back. Caught exceptions are tracked with <see cref="IErrorTracker"/>, and after
    /// a configurable number of retries, the message will be passed to the configured <see cref="IErrorHandler"/>.
    /// </summary>
    [StepDocumentation(@"Wraps the invocation of the entire receive pipeline in an exception handler, tracking the number of times the received message has been attempted to be delivered.

If the maximum number of delivery attempts is reached, the message is passed to the error handler, which by default will move the message to the error queue.")]
    public class ArkDefaultRetryStep : IRetryStep
    {
        /// <summary>
        /// Key of a step context item that indicates that the message must be wrapped in a <see cref="FailedMessageWrapper{TMessage}"/> after being deserialized
        /// </summary>
        public const string DispatchAsFailedMessageKey = "dispatch-as-failed-message";

        readonly RetryStrategySettings _retryStrategySettings;
        readonly IExceptionInfoFactory _exceptionInfoFactory;
        readonly CancellationToken _cancellationToken;
        readonly IFailFastChecker _failFastChecker;
        readonly IErrorHandler _errorHandler;
        readonly IErrorTracker _errorTracker;
        readonly ILog _log;

        /// <summary>
        /// Creates the step
        /// </summary>
        public ArkDefaultRetryStep(IRebusLoggerFactory rebusLoggerFactory, IErrorHandler errorHandler, IErrorTracker errorTracker, IFailFastChecker failFastChecker, IExceptionInfoFactory exceptionInfoFactory, RetryStrategySettings retryStrategySettings, CancellationToken cancellationToken)
        {
            _log = rebusLoggerFactory?.GetLogger<DefaultRetryStep>() ?? throw new ArgumentNullException(nameof(rebusLoggerFactory));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _errorTracker = errorTracker ?? throw new ArgumentNullException(nameof(errorTracker));
            _failFastChecker = failFastChecker ?? throw new ArgumentNullException(nameof(failFastChecker));
            _exceptionInfoFactory = exceptionInfoFactory ?? throw new ArgumentNullException(nameof(exceptionInfoFactory));
            _retryStrategySettings = retryStrategySettings ?? throw new ArgumentNullException(nameof(retryStrategySettings));
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Executes the entire message processing pipeline in an exception handler, tracking the number of failed delivery using <see cref="IErrorTracker"/>.
        /// Passes the message to the <see cref="IErrorHandler"/> when the max number of delivery attempts has been exceeded.
        /// </summary>
        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            var transactionContext = context.Load<ITransactionContext>() ?? throw new RebusApplicationException("Could not find a transaction context in the current incoming step context");
            var transportMessage = context.Load<TransportMessage>() ?? throw new RebusApplicationException("Could not find a transport message in the current incoming step context");
            var messageId = transportMessage.Headers.GetValueOrNull(Headers.MessageId);

            if (string.IsNullOrWhiteSpace(messageId))
            {
                await _passToErrorHandler(context, _exceptionInfoFactory.CreateInfo(new RebusApplicationException(
                    $"Received message with empty or absent '{Headers.MessageId}' header! All messages must carry" +
                    " an ID. If no ID is present, the message cannot be tracked" +
                    " between delivery attempts, and other stuff would also be much harder to" +
                    " do - therefore, it is a requirement that messages carry an ID.")));

                transactionContext.SetResult(commit: false, ack: true);

                return;
            }

            int? deliveryCountValue = null;

            if (transportMessage.Headers.TryGetValue(Headers.DeliveryCount, out var value) && int.TryParse(value, out var deliveryCount))
            {
                deliveryCountValue = deliveryCount;

                var maxDeliveryAttempts = _retryStrategySettings.SecondLevelRetriesEnabled
                    ? 2 * _retryStrategySettings.MaxDeliveryAttempts
                    : _retryStrategySettings.MaxDeliveryAttempts;

                if (deliveryCount > maxDeliveryAttempts)
                {
                    var exceptions = await _errorTracker.GetExceptions(messageId);
                    var maxDescription = _retryStrategySettings.SecondLevelRetriesEnabled
                        ? $"which is {maxDeliveryAttempts}, i.e. 2 x {_retryStrategySettings.MaxDeliveryAttempts} because 2nd level retries are enabled"
                        : $"which is {maxDeliveryAttempts}";

                    var exceptionMessage = exceptions.Any()
                        ? $"Received message with native delivery count header value = {deliveryCount} thus exceeding MAX number of delivery attempts ({maxDescription})"
                        : $"Received message with native delivery count header value = {deliveryCount} thus exceeding MAX number of delivery attempts ({maxDescription}) – the error tracker did not provide additional information about the errors, which may/may not be because the errors happened on another Rebus instance.";

                    var exceptionInfo = _exceptionInfoFactory.CreateInfo(new RebusApplicationException(exceptionMessage));
                    await _passToErrorHandler(context, _getAggregateException(new[] { exceptionInfo }.Concat(exceptions)));
                    await _errorTracker.CleanUp(messageId);
                    transactionContext.SetResult(commit: false, ack: true);

                    return;
                }
            }

            try
            {
                await next();
                await _handleManualDeadlettering(context);
                transactionContext.SetResult(commit: true, ack: true);

                if (transactionContext is ICanEagerCommit canEagerCommit)
                {
                    await canEagerCommit.CommitAsync();
                }
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                _log.Info("Dispatch of message with ID {messageId} was cancelled", messageId);
                transactionContext.SetResult(commit: false, ack: false);
            }
            catch (Exception exception)
            {
                await _handleException(exception, transactionContext, messageId, context, next, deliveryCountValue);
            }
        }

        async Task _handleException(Exception exception, ITransactionContext transactionContext, string messageId, IncomingStepContext context, Func<Task> next, int? deliveryCountValue)
        {
            if (_failFastChecker.ShouldFailFast(messageId, exception)
                || (deliveryCountValue != null && deliveryCountValue > _retryStrategySettings.MaxDeliveryAttempts)
                )
            {
                // special case - it we're supposed to fail fast, AND 2nd level retries are enabled, AND this is the first delivery attempt, try to dispatch as 2nd level:
                if (_retryStrategySettings.SecondLevelRetriesEnabled)
                {
                    await _errorTracker.MarkAsFinal(messageId);
                    await _errorTracker.RegisterError(messageId, exception);
                    await _dispatchSecondLevelRetry(transactionContext, messageId, context, next);
                    return;
                }

                await _errorTracker.MarkAsFinal(messageId);
                await _errorTracker.RegisterError(messageId, exception);
                await _passToErrorHandler(context, _getAggregateException(new[] { _exceptionInfoFactory.CreateInfo(exception) }));
                await _errorTracker.CleanUp(messageId);
                transactionContext.SetResult(commit: false, ack: true);
                return;
            }

            await _errorTracker.RegisterError(messageId, exception);

            if (!await _errorTracker.HasFailedTooManyTimes(messageId))
            {
                transactionContext.SetResult(commit: false, ack: false);
                return;
            }

            if (_retryStrategySettings.SecondLevelRetriesEnabled)
            {
                await _dispatchSecondLevelRetry(transactionContext, messageId, context, next);
                return;
            }

            var aggregateException = _getAggregateException(await _errorTracker.GetExceptions(messageId));

            await _passToErrorHandler(context, aggregateException);
            await _errorTracker.CleanUp(messageId);
            transactionContext.SetResult(commit: false, ack: true);
        }

        async Task _dispatchSecondLevelRetry(ITransactionContext transactionContext, string messageId, IncomingStepContext context, Func<Task> next)
        {
            try
            {
                await _dispatchSecondLevelRetry(transactionContext, context, next);
                await _handleManualDeadlettering(context);
                transactionContext.SetResult(commit: true, ack: true);
                await _errorTracker.CleanUp(messageId);
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                _log.Info("Dispatch of message with ID {messageId} was cancelled", messageId);
            }
            catch (Exception secondLevelException)
            {
                if (_failFastChecker.ShouldFailFast(messageId, secondLevelException))
                {
                    await _errorTracker.MarkAsFinal(messageId);
                    await _errorTracker.RegisterError(messageId, secondLevelException);
                    await _passToErrorHandler(context, _getAggregateException(new[] { _exceptionInfoFactory.CreateInfo(secondLevelException) }));
                    await _errorTracker.CleanUp(messageId);
                    transactionContext.SetResult(commit: false, ack: true);
                    return;
                }

                var exceptions = await _errorTracker.GetExceptions(messageId);
                await _passToErrorHandler(context, _getAggregateException(exceptions.Concat(new[] { _exceptionInfoFactory.CreateInfo(secondLevelException), })));
                await _errorTracker.CleanUp(messageId);
                transactionContext.SetResult(commit: false, ack: true);
            }
        }

        async Task _handleManualDeadlettering(IncomingStepContext context)
        {
            var manualDeadletterCommand = context.Load<ManualDeadletterCommand>();

            if (manualDeadletterCommand == null) return;

            await _passToErrorHandler(context, ExceptionInfo.FromException(manualDeadletterCommand.Exception));
        }

        static async Task _dispatchSecondLevelRetry(ITransactionContext transactionContext, StepContext context, Func<Task> next)
        {
            if (transactionContext.Items.TryGetValue("outgoing-messages", out var result)
                && result is ConcurrentQueue<OutgoingTransportMessage> outgoingMessages)
            {
                outgoingMessages.Clear();
            }

            context.Save(DispatchAsFailedMessageKey, true);

            await next();
        }

        async Task _passToErrorHandler(StepContext context, ExceptionInfo exception)
        {
            var transactionContext = context.Load<ITransactionContext>() ?? throw new RebusApplicationException("Could not find a transaction context in the current incoming step context");
            var transportMessage = context.Load<TransportMessage>() ?? throw new RebusApplicationException("Could not find a transport message in the current incoming step context");

            await _errorHandler.HandlePoisonMessage(transportMessage, transactionContext, exception);
        }

        static ExceptionInfo _getAggregateException(IEnumerable<ExceptionInfo> exceptions)
        {
            var list = exceptions.ToList();

            return new(typeof(AggregateException).GetSimpleAssemblyQualifiedName(),
                $"{list.Count} unhandled exceptions",
                string.Join(Environment.NewLine + Environment.NewLine, list.Select(e => e.GetFullErrorDescription())),
                DateTimeOffset.Now
            );
        }
    }
}