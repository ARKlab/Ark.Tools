using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ark.Tools.ApplicationInsights
{
    /// <summary>
    /// This class implement a <seealso cref="ITelemetryProcessor"/> to remove
    /// all excessive telemetry for successful requests.
    /// </summary>
    public class UnsampleFailedTelemetriesAndTheirDependenciesProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _next;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ITelemetry>> _operations;
        private readonly ConcurrentDictionary<string, DateTime> _disposedOperations;

        /// <summary>
        /// Gets or sets a value flag indicates whether all exceptions should be
        /// logged even if the operation itself succeeds.
        /// </summary>
        public bool AlwaysLogExceptions { get; set; } = true;

        /// <summary>
        /// Gets or sets a flag that indicates whether failed dependencies
        /// should be logged even if the operation itself succeeds.
        /// </summary>
        public bool AlwaysLogFailedDependencies { get; set; } = true;

        /// <summary>
        /// Gets or sets a value that indicates the duration that a dependency
        /// might last before it is logged (even when the operation itself
        /// succeeds).
        /// </summary>
        public TimeSpan AlwaysTraceDependencyWithDuration { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Gets or sets the severity level from which traces should always be
        /// logged (even when the operation succeeds).
        /// </summary>
        public SeverityLevel MinAlwaysTraceLevel { get; set; } = SeverityLevel.Error;

        /// <summary>
        /// Gets or sets a flag that indicates whether or not telemetry, that
        /// is not linked to an operation, should be logged.
        /// </summary>
        public bool IncludeOperationLessTelemetry { get; set; } = true;

        /// <summary>
        /// Gets or sets a flag that throws an error if telemetry is sent for operation
        /// that have already completed. This might result in memory leaks.
        /// </summary>
        /// <remarks>
        /// This flag should <b>NEVER</b> be set in production environments, because it
        /// will keep track of all previous operation identifiers, so it will create a
        /// memory leak on its own. It's just a helper to allow you to detect problems.
        /// </remarks>
        public bool DebugThrowOnDisposedOperations { get; set; } = false;

        /// <summary>
        /// Gets or sets the string that contains a semi-colon seperated list of operations
        /// that should always be logged.
        /// </summary>
        /// <remarks>
        /// You can use regular expressions with the following syntax: <c>/^CMD:.*</c> to
        /// match all operations that start with "CMD:". Matching operations is not case
        /// sensitive.
        /// </remarks>
        public ICollection<string> AlwaysLogOperations { get; } = new List<string>();

        /// <summary>
        /// Constructor to create the processor.
        /// </summary>
        /// <param name="next">
        /// Next telemetry processor.
        /// </param>
        /// <remarks>
        /// This constructor is called by the AI infrastructure, when the
        /// telemetry processing chain is build.
        /// </remarks>
        public UnsampleFailedTelemetriesAndTheirDependenciesProcessor(ITelemetryProcessor next)
        {
            _next = next;
            _operations = new ConcurrentDictionary<string, ConcurrentQueue<ITelemetry>>();
            _disposedOperations = new ConcurrentDictionary<string, DateTime>();
        }

        /// <summary>
        /// Returns a flag whether or not the telemetry should be forwarded
        /// directly.
        /// </summary>
        /// <param name="item">
        /// The telemetry item.
        /// </param>
        /// <returns>
        /// <c>True</c>> if the telemetry item should be forwarded directly or
        /// <c>False</c> if the telemetry item should be hold back or discarded.
        /// </returns>
        private bool _alwaysForwarded(ITelemetry item)
        {
            if (item is MetricTelemetry)
                return true;

            // Check if we need to log all exceptions
            if (AlwaysLogExceptions && item is ExceptionTelemetry)
                return true;

            // Check if we need to log failed dependencies
            var dependency = item as DependencyTelemetry;
            if (AlwaysLogFailedDependencies && dependency?.Success != null && !dependency.Success.Value)
                return true;

            // Check if we need to log slow dependencies
            if (AlwaysTraceDependencyWithDuration > TimeSpan.Zero && dependency != null && dependency.Duration >= AlwaysTraceDependencyWithDuration)
                return true;

            // Check if we need to log traces (based on the severity level)
            if (item is TraceTelemetry trace && trace.SeverityLevel.HasValue && trace.SeverityLevel.Value >= MinAlwaysTraceLevel)
                return true;

            // The event might be kept until later
            return false;
        }

        /// <summary>
        /// Process a collected telemetry item.
        /// </summary>
        /// <param name="item">
        /// A collected Telemetry item.
        /// </param>
        public void Process(ITelemetry item)
        {
            // Check if the item should be forwarded directly
            if (_alwaysForwarded(item))
            {
                // Send it directly
                _unsample(item);
                _next.Process(item);
                return;
            }

            // Obtain the operation identifier
            var operationId = item.Context.Operation?.Id;
            if (string.IsNullOrEmpty(operationId))
            {
                // No operation identifier
                if (IncludeOperationLessTelemetry)
                    _next.Process(item);
                return;
            }

            // All operations are started via a request
            if (item is RequestTelemetry request)
            {
                var shouldNotSample = (request.Success.HasValue && !request.Success.Value) || AlwaysLogOperations.Any(on => _matchOperation(request.Name, on));

                // Obtain (and remove) the telemetries for this operation
                if (_operations.TryRemove(operationId, out var telemetries))
                {
                    while (telemetries.TryDequeue(out var telemetry))
                    {
                        if (shouldNotSample)
                            _unsample(telemetry);

                        _next.Process(telemetry);
                    }

                    // Add the operation to the list of disposed operations
                    if (DebugThrowOnDisposedOperations)
                        _disposedOperations.TryAdd(operationId, DateTime.UtcNow);
                }

                // Always send the request itself
                if (shouldNotSample)
                    _unsample(item);

                _next.Process(item);
            }
            else
            {
                var telemetries = _operations.GetOrAdd(operationId, key =>
                {
                    if (DebugThrowOnDisposedOperations && this._disposedOperations.TryGetValue(operationId, out var insertDate))
                    {
                        var disposedOperationExc = new InvalidOperationException($"Operation '{operationId}' was already completed at {insertDate:O} (UTC). Telemetry {item} cannot be queued.");
                        var errorTelemetry = new ExceptionTelemetry(disposedOperationExc)
                        {
                            Timestamp = item.Timestamp,
                            SeverityLevel = SeverityLevel.Error
                        };
                        _next.Process(errorTelemetry);
                        throw disposedOperationExc;
                    }

                    return new ConcurrentQueue<ITelemetry>();
                });
                telemetries.Enqueue(item);
            }
        }

        private void _unsample(ITelemetry item)
        {
            if (item is ISupportSampling s)
                s.SamplingPercentage = 100.0;
        }

        private static bool _matchOperation(string operationName, string filterName)
        {
            // Check for an exact match
            if (operationName.Equals(filterName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check for a match using regular expressions
            return filterName.Length > 0 && filterName[0] == '/' && Regex.IsMatch(operationName, filterName.Substring(1), RegexOptions.IgnoreCase);
        }
    }
}
