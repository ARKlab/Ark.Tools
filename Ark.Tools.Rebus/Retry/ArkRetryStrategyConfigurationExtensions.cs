using System;
using System.Threading;

using Rebus.Config;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Pipeline.Send;
using Rebus.Retry;
using Rebus.Retry.ErrorTracking;
using Rebus.Retry.FailFast;
using Rebus.Retry.Simple;
using Rebus.Threading;
using Rebus.Time;
using Rebus.Transport;

namespace Ark.Tools.Rebus.Retry
{
    /// <summary>
    /// Configuration extensions for the simple retry strategy
    /// </summary>
    public static class ArkRetryStrategyConfigurationExtensions
    {
        /// <summary>
        /// Configures the simple retry strategy, using the specified error queue address and number of delivery attempts
        /// </summary>
        /// <param name="optionsConfigurer">(extension method target)</param>
        /// <param name="errorQueueName">Specifies the name of the error queue</param>
        /// <param name="maxDeliveryAttempts">Specifies how many delivery attempts should be made before forwarding a failed message to the error queue</param>
        /// <param name="secondLevelRetriesEnabled">Specifies whether second level retries should be enabled - when enabled, the message will be dispatched wrapped in an <see cref="IFailed{TMessage}"/> after the first <paramref name="maxDeliveryAttempts"/> delivery attempts, allowing a different handler to handle the message. Dispatch of the <see cref="IFailed{TMessage}"/> is subject to the same <paramref name="maxDeliveryAttempts"/> delivery attempts</param>
        /// <param name="errorDetailsHeaderMaxLength">Specifies a MAX length of the error details to be enclosed as the <see cref="Headers.ErrorDetails"/> header. As the enclosed error details can sometimes become very long (especially when using many delivery attempts), depending on the transport's capabilities it might sometimes be necessary to truncate the error details</param>
        /// <param name="errorTrackingMaxAgeMinutes">Specifies the max age of in-mem error trackings, for tracked messages that have not had any activity registered on them.</param>
        /// <param name="errorQueueErrorCooldownTimeSeconds">Specifies the time in seconds that the bus instance will wait if forwarding to the dead-letter queue fails.</param>
        public static void ArkRetryStrategy(this OptionsConfigurer optionsConfigurer,
            string errorQueueName = RetryStrategySettings.DefaultErrorQueueName,
            int maxDeliveryAttempts = RetryStrategySettings.DefaultNumberOfDeliveryAttempts,
            bool secondLevelRetriesEnabled = false,
            int errorDetailsHeaderMaxLength = int.MaxValue,
            int errorTrackingMaxAgeMinutes = RetryStrategySettings.DefaultErrorTrackingMaxAgeMinutes,
            int errorQueueErrorCooldownTimeSeconds = RetryStrategySettings.DefaultErrorQueueErrorCooldownTimeSeconds
        )
        {
            optionsConfigurer.RetryStrategy(
                errorQueueName, 
                maxDeliveryAttempts, 
                secondLevelRetriesEnabled, 
                errorDetailsHeaderMaxLength, 
                errorTrackingMaxAgeMinutes, 
                errorQueueErrorCooldownTimeSeconds);
        }
    }
}