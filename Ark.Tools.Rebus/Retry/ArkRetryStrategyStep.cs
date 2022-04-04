using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Retry;
using Rebus.Retry.FailFast;
using Rebus.Retry.Simple;
using Rebus.Transport;
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable ArgumentsStyleOther
// ReSharper disable SuggestBaseTypeForParameter

namespace Ark.Tools.Rebus.Retry
{
    /// <summary>
    /// Incoming message pipeline step that implements a retry mechanism - if the call to the rest of the pipeline fails,
    /// the exception is caught and the queue transaction is rolled back. Caught exceptions are tracked in-mem, and after
    /// a configurable number of retries, the message will be forwarded to the configured error queue and the rest of the pipeline will not be called
    /// </summary>
    [StepDocumentation(@"Wraps the invocation of the entire receive pipeline in an exception handler, tracking the number of times the received message has been attempted to be delivered.

If the maximum number of delivery attempts is reached, the message is moved to the error queue.")]
    public class ArkRetryStrategyStep : IRetryStrategyStep
    {
        /// <summary>
        /// Key of a step context item that indicates that the message must be wrapped in a <see cref="FailedMessageWrapper{TMessage}"/> after being deserialized
        /// </summary>
        public const string DispatchAsFailedMessageKey = "dispatch-as-failed-message";
        public const string DeliveryCountHeader = "rbs-deliverycount";

        readonly SimpleRetryStrategySettings _arkRetryStrategySettings;
        readonly IErrorTracker _errorTracker;
        readonly IErrorHandler _errorHandler;
        readonly IFailFastChecker _failFastChecker;
        readonly CancellationToken _cancellationToken;
        readonly ILog _logger;

        /// <summary>
        /// Constructs the step, using the given transport and settings
        /// </summary>
        public ArkRetryStrategyStep(SimpleRetryStrategySettings arkRetryStrategySettings, IRebusLoggerFactory rebusLoggerFactory, IErrorTracker errorTracker, IErrorHandler errorHandler, IFailFastChecker failFastChecker, CancellationToken cancellationToken)
        {
            _arkRetryStrategySettings = arkRetryStrategySettings ?? throw new ArgumentNullException(nameof(arkRetryStrategySettings));
            _errorTracker = errorTracker ?? throw new ArgumentNullException(nameof(errorTracker));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _failFastChecker = failFastChecker;
            _logger = rebusLoggerFactory?.GetLogger<ArkRetryStrategyStep>() ?? throw new ArgumentNullException(nameof(rebusLoggerFactory));
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Executes the entire message processing pipeline in an exception handler, tracking the number of failed delivery attempts.
        /// Forwards the message to the error queue when the max number of delivery attempts has been exceeded.
        /// </summary>
        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            var transportMessage = context.Load<TransportMessage>() ?? throw new RebusApplicationException("Could not find a transport message in the current incoming step context");
            var transactionContext = context.Load<ITransactionContext>() ?? throw new RebusApplicationException("Could not find a transaction context in the current incoming step context");

            var messageId = transportMessage.Headers.GetValueOrNull(Headers.MessageId);

            if (string.IsNullOrWhiteSpace(messageId))
            {
                await _moveMessageToErrorQueue(context, transactionContext,
                    new RebusApplicationException($"Received message with empty or absent '{Headers.MessageId}' header! All messages must be" +
                                                  " supplied with an ID . If no ID is present, the message cannot be tracked" +
                                                  " between delivery attempts, and other stuff would also be much harder to" +
                                                  " do - therefore, it is a requirement that messages be supplied with an ID."));

                return;
            }

            _checkFinal(context, true);

            if (_errorTracker.HasFailedTooManyTimes(messageId))
            {
                await _handleError(context, next, messageId);
            }
            else
            {
                await _handle(context, next, messageId, transactionContext, messageId);
            }
        }

        /// <summary>
        /// Check if the current message handling is 'done' and flag MessageId as FINAL.
        /// Support also 2nd-level retries and FailFast.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="beforeTry">true if the check is prior to handling, to bailout before event try.</param>
        /// <remarks>
        /// At the start of the Handling we want to bailout if the current DeliveryCount is greater than the threashold.
        /// DeliveryCount is natively 1-based: first try has count=1 before trying.
        /// When this is the last try we need first to execute.
        /// </remarks>
        private void _checkFinal(IncomingStepContext context, bool beforeTry = false)
        {
            var transportMessage = context.Load<TransportMessage>();
            var messageId = transportMessage.Headers.GetValue(Headers.MessageId);

            int? transportDeliveryCount = null;

            if (transportMessage.Headers.TryGetValue(DeliveryCountHeader, out var dch)
                            && int.TryParse(dch, out var dc))
            {
                transportDeliveryCount = dc;
            }

            _checkFinal(messageId, transportDeliveryCount, beforeTry);

            if (_arkRetryStrategySettings.SecondLevelRetriesEnabled)
            {
                var secondLevelMessageId = GetSecondLevelMessageId(messageId);

                if (transportDeliveryCount != null)
                {
                    // can happen that we have fail-fasted the first-level and we're failing the 2nd-level
                    transportDeliveryCount -= _arkRetryStrategySettings.MaxDeliveryAttempts;
                    if (transportDeliveryCount <= 0)
                        transportDeliveryCount = 1;
                }

                _checkFinal(secondLevelMessageId, transportDeliveryCount, beforeTry);
            }
        }

        private void _checkFinal(string messageId, int? transportDeliveryCount, bool beforeTry)
        {
            var exceptions = _errorTracker.GetExceptions(messageId);

            // +1 as DeliveryCount is 1-based and is charged prior to 'receive'
            var deliveryCountFromExceptions = exceptions.Count() + 1;

            // if transport doesn't has deliveryCount, use the count of the Exceptions.
            var deliveryCount = (transportDeliveryCount ?? 0) > deliveryCountFromExceptions ? transportDeliveryCount : deliveryCountFromExceptions;

            if (beforeTry == true) deliveryCount--;

            if ((deliveryCount >= _arkRetryStrategySettings.MaxDeliveryAttempts
                && exceptions.Any()) || exceptions.Any(x => _failFastChecker.ShouldFailFast(messageId, x)))
            {
                _errorTracker.MarkAsFinal(messageId);
            }
        }

        private async Task _handleError(IncomingStepContext context, Func<Task> next, string handledMessageId)
        {
            var transportMessage = context.Load<TransportMessage>() ?? throw new RebusApplicationException("Could not find a transport message in the current incoming step context");
            var transactionContext = context.Load<ITransactionContext>();
            var messageId = transportMessage.Headers.GetValueOrNull(Headers.MessageId);
            var secondLevelMessageId = GetSecondLevelMessageId(messageId);

            if (!_errorTracker.HasFailedTooManyTimes(handledMessageId)) // let's try again
            {
                transactionContext.Abort();
                return;
            }
            else if (messageId == handledMessageId && _arkRetryStrategySettings.SecondLevelRetriesEnabled && !_errorTracker.HasFailedTooManyTimes(secondLevelMessageId))
            {
                context.Save(DispatchAsFailedMessageKey, true);
                await _handle(context, next, secondLevelMessageId, transactionContext, messageId, secondLevelMessageId);
                return;
            }

            await _handlePoisonMessage(context, transactionContext, messageId, secondLevelMessageId);
        }

        AggregateException _getAggregateException(params string[] ids)
        {
            var exceptions = ids.SelectMany(_errorTracker.GetExceptions).ToArray();

            return new AggregateException($"{exceptions.Length} unhandled exceptions", exceptions);
        }

        /// <summary>
        /// Gets the 2nd level retry surrogate message ID corresponding to <paramref name="messageId"/>
        /// </summary>
        public static string GetSecondLevelMessageId(string messageId) => messageId + "-2nd-level";

        async Task _handle(IncomingStepContext context, Func<Task> next, string identifierToTrackMessageBy, ITransactionContext transactionContext, string messageId, string secondLevelMessageId = null)
        {
            try
            {
                await next();

                await transactionContext.Commit();

                _errorTracker.CleanUp(messageId);

                if (secondLevelMessageId != null)
                {
                    _errorTracker.CleanUp(secondLevelMessageId);
                }

            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                _logger.Info("Dispatch of message with ID {messageId} was cancelled", messageId);

                transactionContext.Abort();
            }
            catch (Exception exception)
            {
                _errorTracker.RegisterError(identifierToTrackMessageBy, exception);
                _checkFinal(context);

                await _handleError(context, next, identifierToTrackMessageBy);
            }

        }

        private async Task _handlePoisonMessage(IncomingStepContext context, ITransactionContext transactionContext, string messageId, string secondLevelMessageId)
        {
            var aggregateException = _getAggregateException(new[] { messageId, secondLevelMessageId }.Where(x => x != null).ToArray());
            await _moveMessageToErrorQueue(context, transactionContext, aggregateException);

            _errorTracker.CleanUp(messageId);
            if (secondLevelMessageId != null)
                _errorTracker.CleanUp(secondLevelMessageId);
        }

        async Task _moveMessageToErrorQueue(IncomingStepContext context, ITransactionContext transactionContext, Exception exception)
        {
            var transportMessage = context.Load<OriginalTransportMessage>().TransportMessage.Clone();
            await _errorHandler.HandlePoisonMessage(transportMessage, transactionContext, exception);
        }

    }
}