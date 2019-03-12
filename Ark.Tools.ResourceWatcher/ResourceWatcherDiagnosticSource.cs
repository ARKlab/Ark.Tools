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

        //Event
        public void RunTookTooLong(TimeSpan elapsed)
        {
            _logger.Fatal($"Check for tenant {_tenant} took too much:{elapsed}");

            _reportException("RunTookTooLong",
                () => new
                {
                    Elapsed = elapsed
                });
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

        //public void CheckStateFailed(Activity activity, Exception ex)
        //{
        //    _stop(activity, () => new
        //    {
        //        Exception = ex,
        //        Tenant = _tenant,
        //    }
        //    );
        //}

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
            Activity activity = _start(".Process", () => new
            {
                Metadata = metadata,
                State = state,
                Tenant = _tenant,
            },
                null
            );

            return activity;
        }

        public void ProcessResourceFailed(Activity activity, Exception ex)
        {
            _stop(activity, () => new
            {
                Exception = ex,
                Tenant = _tenant,
            }
            );
        }

        public void ProcessResourceSuccessful(Activity activity, object payload, ProcessType processType)
        {
            //_logger.Warn
            _setTags(activity, processType.ToString(), processType.ToString());

            _stop(activity, () => new
            {
                Payload = payload,
                Tenant = _tenant,
            }
            );
        }

        //Event
        public void ProcessResourceTookTooLong(string resourceId, TimeSpan elapsed)
        {
            _logger.Fatal($"Processing of ResourceId=\"{resourceId}\" took too much: {elapsed}");

            _reportException("ProcessResourceTookTooLong",
                () => new
                {
                    Elapsed = elapsed
                });
        }
        #endregion

        #region Exception
        public void ThrowInvalidOperationException(string badKey)
        {
            var ex = new InvalidOperationException($"Found multiple entries for ResouceId:{badKey}");

            _reportException("ThrowInvalidOperationException", () => ex);

            throw ex;
        }

        public void ThrowDuplicateResourceIdRetrived(string activityName, string duplicateId)
        {
            var ex = new ApplicationException("");

            _reportException("DuplicateResourceIdRetrived", () => ex);

            throw ex;            
        }

        public void ReportRunConsecutiveFailureLimitReached(Exception lastEx, int count)
        {
            _logger.Fatal($"Failed {count} times consecutively");

            _reportException(BaseActivityName + ".Run", () => lastEx);
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

        internal void _reportException(string eventName, Func<object> getPayload)
        {
            if (_source.IsEnabled(eventName))
            {
                _source.Write(eventName,
                    new
                    {
                        Exception = getPayload(),
                        Tenant = _tenant
                    });
            }
        }
    }
}