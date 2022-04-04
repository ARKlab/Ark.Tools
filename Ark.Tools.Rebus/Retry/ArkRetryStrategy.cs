using System;
using System.Threading;

using Rebus.Logging;
using Rebus.Retry;
using Rebus.Retry.FailFast;
using Rebus.Retry.Simple;

namespace Ark.Tools.Rebus.Retry
{
    /// <summary>
    /// Implementation of <see cref="IRetryStrategy"/> that tracks errors in memory
    /// </summary>
    public class ArkRetryStrategy : IRetryStrategy
    {
        readonly SimpleRetryStrategySettings _simpleRetryStrategySettings;
        readonly IRebusLoggerFactory _rebusLoggerFactory;
        readonly IErrorTracker _errorTracker;
        readonly IErrorHandler _errorHandler;
        readonly IFailFastChecker _failFastChecker;
        readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Constructs the retry strategy with the given settings, creating an error queue with the configured name if necessary
        /// </summary>
        public ArkRetryStrategy(SimpleRetryStrategySettings simpleRetryStrategySettings, IRebusLoggerFactory rebusLoggerFactory, IErrorTracker errorTracker, IErrorHandler errorHandler, IFailFastChecker failFastChecker, CancellationToken cancellationToken)
        {
            _simpleRetryStrategySettings = simpleRetryStrategySettings ?? throw new ArgumentNullException(nameof(simpleRetryStrategySettings));
            _rebusLoggerFactory = rebusLoggerFactory ?? throw new ArgumentNullException(nameof(rebusLoggerFactory));
            _errorTracker = errorTracker ?? throw new ArgumentNullException(nameof(errorTracker));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _failFastChecker = failFastChecker;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the retry step with appropriate settings for this <see cref="ArkRetryStrategy"/>
        /// </summary>
        public IRetryStrategyStep GetRetryStep() => new ArkRetryStrategyStep(
            _simpleRetryStrategySettings,
            _rebusLoggerFactory,
            _errorTracker,
            _errorHandler,
            _failFastChecker,
            _cancellationToken
        );
    }
}