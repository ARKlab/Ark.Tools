using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Rebus.Pipeline;
using Rebus.Retry;
using Rebus.Retry.Simple;

namespace Ark.Tools.Rebus.Retry
{
    public class ErrorTrackerNativeCountDecorator

        : IErrorTracker
    {
        private readonly IErrorTracker _errorTracker;
        private readonly RetryStrategySettings _retryStrategySettings;

        public const string DeliveryCountHeader = "rbs-deliverycount";

        public ErrorTrackerNativeCountDecorator(IErrorTracker errorTracker, RetryStrategySettings retryStrategySettings)
        {
            _errorTracker = errorTracker;
            _retryStrategySettings = retryStrategySettings;
        }

        public Task CleanUp(string messageId)
        {
            return _errorTracker.CleanUp(messageId);
        }

        public Task<IReadOnlyList<ExceptionInfo>> GetExceptions(string messageId)
        {
            return _errorTracker.GetExceptions(messageId);
        }

        public Task<string> GetFullErrorDescription(string messageId)
        {
            return _errorTracker.GetFullErrorDescription(messageId);
        }

        public async Task<bool> HasFailedTooManyTimes(string messageId)
        {
            return await _errorTracker.HasFailedTooManyTimes(messageId) || _hasTransportDeliveredTooManyTimes();
        }

        private bool _hasTransportDeliveredTooManyTimes()
        {
            var transportMessage = MessageContext.Current.TransportMessage;
            
            if (transportMessage.Headers.TryGetValue(DeliveryCountHeader, out var dch)
                            && int.TryParse(dch, out var dc))
            {
                return dc > _retryStrategySettings.MaxDeliveryAttempts + (_retryStrategySettings.SecondLevelRetriesEnabled ? _retryStrategySettings.MaxDeliveryAttempts : 0);
            }
            return false;
        }

        public Task MarkAsFinal(string messageId)
        {
            return _errorTracker.MarkAsFinal(messageId);
        }

        public Task RegisterError(string messageId, Exception exception)
        {
            return _errorTracker.RegisterError(messageId, exception);
        }
    }
}