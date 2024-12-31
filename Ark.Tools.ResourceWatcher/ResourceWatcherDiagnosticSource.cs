// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Ark.Tools.ResourceWatcher
{
    internal sealed class ResourceWatcherDiagnosticSource
    {
        public const string DiagnosticListenerName = "Ark.Tools.ResourceWatcher";
        public const string BaseActivityName = "Ark.Tools.ResourceWatcher";
        public const string ExceptionEventName = BaseActivityName + "Exception";

        private readonly string _tenant;
        private readonly Logger _logger;

        private static readonly DiagnosticListener _source = new(DiagnosticListenerName);

        public ResourceWatcherDiagnosticSource(string tenant, Logger logger)
        {
            _tenant = tenant;
            _logger = logger;
        }

        #region Event
        public void HostStartEvent()
        {
            _reportEvent("HostStartEvent", () => new { });
        }

        public void RunTookTooLong(Activity activity)
        {
            _logger.Fatal($"Check for tenant {_tenant} took too much:{activity.Duration}");

            _reportEvent("RunTookTooLong",
                () => new
                {
                    Activity = activity,
                    Tenant = _tenant,
                });
        }

        public void ProcessResourceTookTooLong(string resourceId, Activity activity)
        {
            _logger.Fatal($"Processing of ResourceId={resourceId} took too much: {activity.Duration}");

            _reportEvent("ProcessResourceTookTooLong",
                () => new
                {
                    ResourceId = resourceId,
                    Activity = activity,
                    Tenant = _tenant,
                });
        }
        #endregion

        #region Run
        public Activity RunStart(RunType type, DateTime now)
        {
            _logger.Info(CultureInfo.InvariantCulture, "Check started for tenant {Tenant} at {Now}", _tenant, now);

            Activity activity = _start("Run", () => new
            {
                Type = type,
                Now = now,
                Tenant = _tenant,
            }
            );

            return activity;
        }

        public void RunFailed(Activity activity, Exception ex)
        {
            _stop(activity, () => new
            {
                Exception = ex,
                Elapsed = activity.Duration,
                Tenant = _tenant,
            }
            );

            _logger.Error(ex, $"Check failed for tenant {_tenant} in {activity.Duration}");
        }

        public void RunSuccessful(Activity activity, IList<ProcessContext> evaluated)
        {
            _stop(activity, () =>
            {
                var total = 0;
                var counts = evaluated
                    .GroupBy(x => x.ResultType ?? throw new InvalidOperationException("ResultType is null"))
                    .ToDictionary(x => x.Key, x => x.Count());
                foreach (var k in Enum.GetValues(typeof(ResultType)).Cast<ResultType>())
                {
                    if (!counts.ContainsKey(k))
                        counts[k] = 0;

                    total += counts[k];
                }

                return new
                {
                    ResourcesFound = total,
                    Normal = counts[ResultType.Normal],
                    NoNewData = counts[ResultType.NoNewData],
                    NoAction = counts[ResultType.NoAction],
                    Error = counts[ResultType.Error],
                    Skipped = counts[ResultType.Skipped],
                    Tenant = _tenant,
                };
            }
            );

            _logger.Info(CultureInfo.InvariantCulture, "Check successful for tenant {Tenant} in {Duration}", _tenant, activity?.Duration);
        }
        #endregion

        #region GetResources
        public Activity GetResourcesStart()
        {
            Activity activity = _start("GetResources", () => new
            {
            }
            );

            return activity;
        }

        public void GetResourcesFailed(Activity activity, Exception ex)
        {
            _stop(activity, () => new
            {
                Exception = ex,
                Tenant = _tenant,
            }
            );
        }

        public void GetResourcesSuccessful(Activity activity, int count)
        {
            _stop(activity, () => new
            {
                ResourcesFound = count,
                Elapsed = activity.Duration,
                Tenant = _tenant,
            }
            );

            _logger.Info(CultureInfo.InvariantCulture, "Found {ResourceCount} resources in {Duration}", count, activity?.Duration);
        }
        #endregion

        #region CheckState
        public Activity CheckStateStart()
        {
            Activity activity = _start("CheckState", () => new
            {
            }
            );

            return activity;
        }

        public void CheckStateSuccessful(Activity activity, IEnumerable<ProcessContext> evaluated)
        {
            _stop(activity, () =>
            {
                var counts = evaluated.GroupBy(x => x.ProcessType).ToDictionary(x => x.Key, x => x.Count());
                foreach (var k in Enum.GetValues(typeof(ProcessType)).Cast<ProcessType>())
                    if (!counts.ContainsKey(k))
                        counts[k] = 0;

                return new
                {
                    ResourcesNew = counts[ProcessType.New],
                    ResourcesUpdated = counts[ProcessType.Updated],
                    ResourcesRetried = counts[ProcessType.Retry],
                    ResourcesRetriedAfterBan = counts[ProcessType.RetryAfterBan],
                    ResourcesBanned = counts[ProcessType.Banned],
                    ResourcesNothingToDo = counts[ProcessType.NothingToDo],
                    Tenant = _tenant,
                };
            }
            );
        }


        public void CheckStateFailed(Activity activity, Exception ex)
        {
            _stop(activity, () => new
            {
                Exception = ex,
                Tenant = _tenant,
            }
            );
        }
        #endregion

        #region ProcessResource
        public Activity ProcessResourceStart(ProcessContext processContext)
        {
            bool result = processContext.IsResourceUpdated(out var infos);

            if (!result)
            {
                _logger.Info(CultureInfo.InvariantCulture, "No changes detected on ResourceId={ResourceId}"
                 , processContext.CurrentInfo.ResourceId
                );
            }
            else
            {
                _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) Detected change on ResourceId={ResourceId}, Resource.ModifiedSource={ModifiedSource}, Resource.Modified={Modified}, OldState.Modified={OldModified}, OldState.Retry={OldRetryCount}. Processing..."
                    , processContext.Index
                    , processContext.Total
                    , processContext.CurrentInfo.ResourceId
                    , infos.source ?? string.Empty
                    , infos.current?.ToString()
                    , infos.last?.ToString()
                    , processContext.LastState?.RetryCount
                );
            }

            Activity activity = _start("ProcessResource", () => new
            {
                ProcessContext = processContext,
                Tenant = _tenant,
            });

            return activity;
        }

        public void ProcessResourceFailed(Activity activity, ProcessContext pc, bool isBanned, Exception ex)
        {
            var lvl = isBanned ? LogLevel.Fatal : LogLevel.Warn;
            _logger.Log(lvl, ex, CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} process Failed", pc.Index , pc.Total, pc.CurrentInfo.ResourceId);

            _stop(activity, () => new
            {
                ProcessContext = pc,
                Exception = ex,
                Tenant = _tenant,
            });
        }

        public void ProcessResourceSuccessful(Activity activity, ProcessContext pc)
        {
            _stop(activity, () => new
            {
                ProcessContext = pc,
                Tenant = _tenant,
            });

            if (pc.ResultType == ResultType.NoNewData)
            {
                _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} No payload retrived, so no new state. Generally due to a same-checksum", pc.Index, pc.Total, pc.CurrentInfo.ResourceId);
            }
            else if (pc.ResultType == ResultType.NoAction)
            {
                _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} No action has been triggered and payload has not been retrieved. We do not change the state", pc.Index, pc.Total, pc.CurrentInfo.ResourceId);
            }
            else if (pc.ResultType == ResultType.Normal)
            {
                if (pc.NewState?.RetryCount == 0)
                    _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} handled successfully in {Duration}", pc.Index, pc.Total, pc.CurrentInfo.ResourceId, activity?.Duration);
                else
                    _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} handled not successfully in {Duration}", pc.Index, pc.Total, pc.CurrentInfo.ResourceId, activity?.Duration);
            }
        }
        #endregion

        #region FetchResource
        public Activity FetchResourceStart(ProcessContext pc)
        {
            Activity activity = _start("FetchResource", () => new
            {
                ProcessContext = pc,
                Tenant = _tenant,
            }
            );

            return activity;
        }

        public void FetchResourceFailed(Activity activity, ProcessContext pc, Exception ex)
        {
            _stop(activity, () => new
            {
                ProcessContext = pc,
                Exception = ex,
                Tenant = _tenant,
            }
            );
        }

        public void FetchResourceSuccessful(Activity activity, ProcessContext pc)
        {
            //_setTags(activity, processType.ToString(), processType.ToString());

            _stop(activity, () => new
            {
                ProcessContext = pc,
                Tenant = _tenant,
            }
            );
        }
        #endregion


        #region Exception
        public void ProcessResourceSaveFailed(string resourceId, Exception ex)
        {
            _logger.Error(ex, $"Saving of ResourceId={resourceId} failed");

            _reportException("ProcessResourceSaveFailed", ex);
        }

        public void ThrowDuplicateResourceIdRetrived(string duplicateId)
        {
            var ex = new InvalidOperationException($"Found multiple entries for ResouceId: {duplicateId}");

            _reportException("ThrowDuplicateResourceIdRetrived", ex);

            throw ex;            
        }

        public void ReportRunConsecutiveFailureLimitReached(Exception ex, int count)
        {
            _logger.Fatal($"Failed {count} times consecutively");

            _reportException("ReportRunConsecutiveFailureLimitReached", ex);
        }
        #endregion

        private Activity _start(string operationName, Func<object> getPayload, bool unlinkFromParent = false)
        {
            string activityName = BaseActivityName + "." + operationName;

            var activity = new Activity(activityName);

            if (_source.IsEnabled(activityName + ".Start"))
            {
                _source.StartActivity(activity, getPayload());
            }
            else
            {
                activity.Start();
            }

            return activity;
        }

        private void _stop(Activity activity, Func<object> getPayload)
        {
            if (activity != null)
            {
                _source.StopActivity(activity, getPayload());
            }
        }

        private void _reportEvent(string eventName, Func<object> getPayload)
        {
            var name = BaseActivityName + "." + eventName;

            if (_source.IsEnabled(name))
            {
                _source.Write(name, getPayload());
            }
        }

        private void _reportException(string exceptionName, Exception ex)
        {
            var name = BaseActivityName + "." + exceptionName;

            if (_source.IsEnabled(name))
            {
                _source.Write(name,
                    new
                    {
                        Exception = ex,
                        Tenant = _tenant
                    });
            }
        }
    }
}