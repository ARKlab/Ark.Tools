// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DiagnosticAdapter;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ark.Tools.ResourceWatcher.Testing
{
    /// <summary>
    /// A diagnostic listener that captures ResourceWatcher processing decisions for test assertions.
    /// Subscribe this listener explicitly in test setup to capture ProcessType, ResultType, and exceptions per resource.
    /// </summary>
    public class TestingDiagnosticListener : ResourceWatcherDiagnosticListenerBase
    {
        private readonly ConcurrentDictionary<string, ResourceProcessingResult> _results = new(StringComparer.Ordinal);
        private readonly ConcurrentBag<RunResult> _runResults = new();
        private readonly ConcurrentBag<CheckStateResult> _checkStateResults = new();

        /// <summary>
        /// Gets all captured processing results by resource ID.
        /// </summary>
        public IReadOnlyDictionary<string, ResourceProcessingResult> Results => _results;

        /// <summary>
        /// Gets all captured run results.
        /// </summary>
        public IReadOnlyList<RunResult> RunResults => _runResults.ToList();

        /// <summary>
        /// Gets the latest check state result.
        /// </summary>
        public CheckStateResult? LatestCheckStateResult => _checkStateResults.LastOrDefault();

        /// <summary>
        /// Clears all captured results.
        /// </summary>
        public void Clear()
        {
            _results.Clear();
            _runResults.Clear();
            while (_checkStateResults.TryTake(out _)) { }
        }

        /// <summary>
        /// Simulates a resource being processed for testing purposes.
        /// Use this to manually add processing results when testing outside of the actual ResourceWatcher.
        /// </summary>
        /// <param name="resourceId">The resource ID.</param>
        /// <param name="processType">The process type.</param>
        /// <param name="resultType">The result type.</param>
        /// <param name="tenant">The tenant (defaults to "default").</param>
        /// <param name="exception">Optional exception if processing failed.</param>
        public void SimulateProcessed(string resourceId, ProcessType processType, ResultType resultType, string tenant = "default", Exception? exception = null)
        {
            var result = new ResourceProcessingResult
            {
                Tenant = tenant,
                ResourceId = resourceId,
                ProcessType = processType,
                ResultType = resultType,
                Exception = exception
            };
            _results.AddOrUpdate(resourceId, result, (k, v) => result);
        }

        /// <summary>
        /// Gets the processing result for a specific resource.
        /// </summary>
        public ResourceProcessingResult? GetResult(string resourceId)
        {
            _results.TryGetValue(resourceId, out var result);
            return result;
        }

        /// <summary>
        /// Gets all resources processed with a specific ProcessType.
        /// </summary>
        public IReadOnlyList<string> GetResourcesByProcessType(ProcessType processType)
        {
            return _results.Where(r => r.Value.ProcessType == processType).Select(r => r.Key).ToList();
        }

        /// <summary>
        /// Gets all resources processed with a specific ResultType.
        /// </summary>
        public IReadOnlyList<string> GetResourcesByResultType(ResultType resultType)
        {
            return _results.Where(r => r.Value.ResultType == resultType).Select(r => r.Key).ToList();
        }

        /// <summary>
        /// Gets count of resources by ProcessType.
        /// </summary>
        public int CountByProcessType(ProcessType processType)
        {
            return _results.Count(r => r.Value.ProcessType == processType);
        }

        /// <summary>
        /// Gets count of resources by ResultType.
        /// </summary>
        public int CountByResultType(ResultType resultType)
        {
            return _results.Count(r => r.Value.ResultType == resultType);
        }

        #region Diagnostic Event Handlers

        [DiagnosticName("Ark.Tools.ResourceWatcher.CheckState.Stop")]
        public override void OnCheckStateStop(
            int resourcesNew,
            int resourcesUpdated,
            int resourcesRetried,
            int resourcesRetriedAfterBan,
            int resourcesBanned,
            int resourcesNothingToDo,
            string tenant,
            Exception exception)
        {
            _checkStateResults.Add(new CheckStateResult
            {
                Tenant = tenant,
                ResourcesNew = resourcesNew,
                ResourcesUpdated = resourcesUpdated,
                ResourcesRetried = resourcesRetried,
                ResourcesRetriedAfterBan = resourcesRetriedAfterBan,
                ResourcesBanned = resourcesBanned,
                ResourcesNothingToDo = resourcesNothingToDo,
                Exception = exception
            });
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResource.Stop")]
        public override void OnProcessResourceStop(string tenant, ProcessContext processContext, Exception exception)
        {
            var result = new ResourceProcessingResult
            {
                Tenant = tenant,
                ResourceId = processContext.CurrentInfo.ResourceId,
                ProcessType = processContext.ProcessType,
                ResultType = processContext.ResultType,
                Exception = exception,
                Index = processContext.Index,
                Total = processContext.Total
            };

            _results.AddOrUpdate(processContext.CurrentInfo.ResourceId, result, (k, v) => result);
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Stop")]
        public override void OnRunStop(
            int resourcesFound,
            int normal,
            int noPayload,
            int noAction,
            int error,
            int skipped,
            string tenant,
            Exception exception)
        {
            _runResults.Add(new RunResult
            {
                Tenant = tenant,
                ResourcesFound = resourcesFound,
                Normal = normal,
                NoPayload = noPayload,
                NoAction = noAction,
                Error = error,
                Skipped = skipped,
                Exception = exception
            });
        }

        #endregion
    }

    /// <summary>
    /// Represents the processing result for a single resource.
    /// </summary>
    public sealed class ResourceProcessingResult
    {
        public required string Tenant { get; init; }
        public required string ResourceId { get; init; }
        public ProcessType ProcessType { get; init; }
        public ResultType? ResultType { get; init; }
        public Exception? Exception { get; init; }
        public int? Index { get; init; }
        public int? Total { get; init; }
    }

    /// <summary>
    /// Represents the result of a check state operation.
    /// </summary>
    public sealed class CheckStateResult
    {
        public required string Tenant { get; init; }
        public int ResourcesNew { get; init; }
        public int ResourcesUpdated { get; init; }
        public int ResourcesRetried { get; init; }
        public int ResourcesRetriedAfterBan { get; init; }
        public int ResourcesBanned { get; init; }
        public int ResourcesNothingToDo { get; init; }
        public Exception? Exception { get; init; }
    }

    /// <summary>
    /// Represents the result of a run operation.
    /// </summary>
    public sealed class RunResult
    {
        public required string Tenant { get; init; }
        public int ResourcesFound { get; init; }
        public int Normal { get; init; }
        public int NoPayload { get; init; }
        public int NoAction { get; init; }
        public int Error { get; init; }
        public int Skipped { get; init; }
        public Exception? Exception { get; init; }
    }
}
