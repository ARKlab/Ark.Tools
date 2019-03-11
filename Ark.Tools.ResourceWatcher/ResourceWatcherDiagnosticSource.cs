using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher
{
    internal class ResourceWatcherDiagnosticSource
    {
        public const string DiagnosticListenerName = "Ark.Tools.ResourceWatcher";
        public const string BaseActivityName = "Ark.Tools.ResourceWatcher";

        private readonly string _tenant;
        private static readonly DiagnosticListener _source = new DiagnosticListener(DiagnosticListenerName);

        public ResourceWatcherDiagnosticSource(string tenant)
        {
            _tenant = tenant;
        }

        #region Run
        public Activity RunStart(string type)
        {
            Activity activity = _start(".Run", () => new
            {
                Type = type,
                Tenant = _tenant,
            },
                null
            );

            return activity;
        }

        public void RunFailed(Activity activity, Exception ex)
        {
            _stop(activity, () => new
            {
                Exception = ex,
                Tenant = _tenant,
            }
            );
        }

        public void RunSuccessful(Activity activity, int processedResourcesCount)
        {
            _stop(activity, () => new
            {
                ProcessedResourceCount = processedResourcesCount,
                Tenant = _tenant,
            }
            );
        }
        #endregion

        #region GetResources
        public Activity GetResourcesStart()
        {
            Activity activity = _start(".GetResources", null,
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

        public void GetResourcesSuccessful(Activity activity, int count)
        {
            _stop(activity, () => new
            {
                ResourcesFound = count,
                Tenant = _tenant,
            }
            );
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

        //internal Activity StartOperation<TStart>(string operationName, TStart payload)
        //{
        //    Activity activity = null;
        //    string activityName = BaseActivityName + operationName;

        //    if (_source.IsEnabled())
        //    {
        //        activity = new Activity(activityName);

        //        if (_source.IsEnabled(activityName + ".Start"))
        //            _source.StartActivity(activity, new { Payload = payload, Entity = this._tenant });
        //        else
        //            activity.Start();
        //    }

        //    return activity;
        //}

        //internal void StopOperation<TStop>(Activity activity, TStop payload)
        //{
        //    if (activity != null)
        //    {
        //        if (_source.IsEnabled(activity.OperationName + ".Stop"))
        //            _source.StopActivity(activity, new
        //            {
        //                Payload = payload,
        //                Entity = this._tenant
        //            });
        //        else
        //            activity.Stop();
        //    }
        //}

    }
}