﻿using Rebus.Logging;
using Rebus.Retry;
using Rebus.Retry.FailFast;
using Rebus.Retry.Simple;

using System;
using System.Threading;

namespace Ark.Tools.Rebus.Retry
{
    public class ArkDefaultRetryStrategy : IRetryStrategy
    {
        readonly RetryStrategySettings _retryStrategySettings;
        readonly IRebusLoggerFactory _rebusLoggerFactory;
        readonly IErrorTracker _errorTracker;
        readonly IErrorHandler _errorHandler;
        readonly IFailFastChecker _failFastChecker;
        readonly IExceptionInfoFactory _exceptionInfoFactory;
        readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Constructs the retry strategy with the given settings, creating an error queue with the configured name if necessary
        /// </summary>
        public ArkDefaultRetryStrategy(RetryStrategySettings retryStrategySettings, IRebusLoggerFactory rebusLoggerFactory, IErrorTracker errorTracker, IErrorHandler errorHandler, IFailFastChecker failFastChecker, IExceptionInfoFactory exceptionInfoFactory, CancellationToken cancellationToken)
        {
            _retryStrategySettings = retryStrategySettings ?? throw new ArgumentNullException(nameof(retryStrategySettings));
            _rebusLoggerFactory = rebusLoggerFactory ?? throw new ArgumentNullException(nameof(rebusLoggerFactory));
            _errorTracker = errorTracker ?? throw new ArgumentNullException(nameof(errorTracker));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _failFastChecker = failFastChecker ?? throw new ArgumentNullException(nameof(failFastChecker));
            _exceptionInfoFactory = exceptionInfoFactory ?? throw new ArgumentNullException(nameof(exceptionInfoFactory));
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the retry step with appropriate settings for this <see cref="DefaultRetryStrategy"/>
        /// </summary>
        public IRetryStep GetRetryStep() => new ArkDefaultRetryStep(
            _rebusLoggerFactory,
            _errorHandler,
            _errorTracker,
            _failFastChecker,
            _exceptionInfoFactory,
            _retryStrategySettings,
            _cancellationToken
        );
    }
}