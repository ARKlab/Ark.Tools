using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher
{
    internal class ResourceWatcherDiagnosticSource
    {
        public const string DiagnosticListenerName = "Ark.Tools.ResourceWatcher";
        public const string BaseActivityName = "Ark.Tools.ResourceWatcher";
        public const string ExceptionEventName = BaseActivityName + "Exception";

        private readonly string _tenant;
        private static Logger _logger;

        private static readonly DiagnosticListener _source = new DiagnosticListener(DiagnosticListenerName);

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

        public void RunTookTooLong(TimeSpan elapsed)
        {
            _logger.Fatal($"Check for tenant {_tenant} took too much:{elapsed}");

            _reportEvent("RunTookTooLong",
                () => new
                {
                    Elapsed = elapsed,
                    Tenant = _tenant,
                });
        }

        public void ProcessResourceTookTooLong(string resourceId, TimeSpan elapsed)
        {
            _logger.Fatal($"Processing of ResourceId=\"{resourceId}\" took too much: {elapsed}");

            _reportEvent("ProcessResourceTookTooLong",
                () => new
                {
                    ResourceId = resourceId,
                    Elapsed = elapsed,
                    Tenant = _tenant,
                });
        }
        #endregion

        #region Run
        public Activity RunStart(RunType type, DateTime now)
        {
            _logger.Info($"Check started for tenant {_tenant} at {now}");

            Activity activity = _start(".Run", () => new
            {
                Type = type,
                Now = now,
                Tenant = _tenant,
            },
                null
            );

            return activity;
        }

        public void RunFailed(Activity activity, Exception ex, TimeSpan elapsed)
        {
            _logger.Error(ex, $"Check failed for tenant {_tenant} in {elapsed}");

            _stop(activity, () => new
            {
                Exception = ex,
                Elapsed = elapsed,
                Tenant = _tenant,
            }
            );
        }

        public void RunSuccessful(Activity activity, List<ProcessData> toProcess, TimeSpan elapsed)
        {
            _logger.Info($"Check successful for tenant {_tenant} in {elapsed}");

            _stop(activity, () => new
            {
                TotalResources = toProcess.Count,
                NewResources = toProcess.Where(w => w.ProcessDataType == ProcessDataType.New).Count(),
                Tenant = _tenant,
            }
            );
        }
        #endregion

        #region GetResources
        public Activity GetResourcesStart()
        {
            Activity activity = _start(".GetResources", () => new
            {
            },
                null
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

        public void GetResourcesSuccessful(Activity activity, int count, TimeSpan elapsed)
        {
            _logger.Info($"Found {count} resources in {elapsed}");

            _stop(activity, () => new
            {
                ResourcesFound = count,
                Elapsed = elapsed,
                Tenant = _tenant,
            }
            );
        }
        #endregion

        #region CheckState
        public Activity CheckStateStart()
        {
            Activity activity = _start(".CheckState", () => new
            {
            },
                null
            );

            return activity;
        }

        public void CheckStateSuccessful(Activity activity, IEnumerable<ProcessData> toEvaluate)
        {
            _stop(activity, () =>
            {
                var counts = toEvaluate.GroupBy(x => x.ProcessDataType).ToDictionary(x => x.Key, x => x.Count());
                foreach (var k in Enum.GetValues(typeof(ProcessDataType)).Cast<ProcessDataType>())
                    if (!counts.ContainsKey(k))
                        counts[k] = 0;

                return new
                {
                    ResourcesNew = counts[ProcessDataType.New],
                    ResourcesUpdated = counts[ProcessDataType.Updated],
                    ResourcesRetried = counts[ProcessDataType.Retry],
                    ResourcesRetriedAfterBan = counts[ProcessDataType.RetryAfterBan],
                    Tenant = _tenant,
                };
            }
            );
        }
        #endregion

        #region ProcessResource
        public Activity ProcessResourceStart(IResourceMetadata metadata, ResourceState state)
        {
            Activity activity = _start(".ProcessResource", () => new
            {
                Metadata = metadata,
                State = state,
                Tenant = _tenant,
            },
                null
            );

            return activity;
        }

        public void ProcessResourceFailed(Activity activity, LogLevel lvl, string resourceId, ProcessDataType processDataType, ProcessType processType, Exception ex)
        {
            _logger.Log(lvl, ex, $"Error while processing ResourceId=\"{resourceId}\"");

            _stop(activity, () => new
            {
                ProcessDataType = processDataType,
                ResourceIdId = resourceId,
                Exception = ex,
                Tenant = _tenant,
            }
            );
        }

        public void ProcessResourceSuccessful(    Activity activity
                                                , int idx
                                                , string resourceId
                                                , TimeSpan elapsed
                                                , int retryCount
                                                , int total
                                                , IResourceState state
                                                , ProcessDataType processDataType
                                                , ProcessType processType)
        {
            if (processType == ProcessType.NoPayload)
            {
                _logger.Info($"({idx}/{total}) ResourceId=\"{resourceId}\" No payload retrived, so no new state. Generally due to a same-checksum");
            }
            else if (processType == ProcessType.NoAction)
            {
                _logger.Info($"({idx}/{total}) ResourceId=\"{resourceId}\" No action has been triggered and payload has not been retrieved. We do not change the state");
            }
            else if (processType == ProcessType.Normal)
            {
                _logger.Info($"({idx}/{total}) ResourceId=\"{resourceId}\" handled {(retryCount == 0 ? "" : "not ")}successfully in {elapsed}");
            }

            //_setTags(activity, processType.ToString(), processType.ToString());

            _stop(activity, () => new
            {
                ProcessDataType = processDataType,
                ResourceIdId = resourceId,
                State = state,
                Tenant = _tenant,
            }
            );
        }
        #endregion

        #region Exception
        public void ProcessResourceSaveFailed(string resourceId, Exception ex)
        {
            _logger.Error(ex, $"Saving of ResourceId=\"{resourceId}\" failed");

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

        private Activity _start(string operationName, Func<object> getPayload, Action<Activity> setTags)
        {
            Activity activity = null;
            string activityName = BaseActivityName + operationName;

            if (_source.IsEnabled(activityName))
            {
                activity = new Activity(activityName);
                setTags?.Invoke(activity);

                if (_source.IsEnabled(activityName + ".Start"))
                {
                    _source.StartActivity(activity, getPayload());
                }
                else
                {
                    activity.Start();
                }
            }

            return activity;
        }

        internal void _stop(Activity activity, Func<object> getPayload)
        {
            if (activity != null)
            {
                _source.StopActivity(activity, getPayload());
            }
        }

        private void _setTags(Activity activity, string messageKey, string message)
        {
            if (activity != null)
            {
                if (message == null)
                {
                    return;
                }

                activity.AddTag(messageKey, message);
            }
        }

        internal void _reportEvent(string eventName, Func<object> getPayload)
        {
            var name = BaseActivityName + "." + eventName;

            if (_source.IsEnabled(name))
            {
                _source.Write(name, getPayload());
            }
        }

        internal void _reportException(string exceptionName, Exception ex)
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