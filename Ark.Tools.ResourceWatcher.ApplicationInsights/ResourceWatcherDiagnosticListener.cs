using Ark.Tools.Nodatime.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DiagnosticAdapter;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{ 
    public class ResourceWatcherDiagnosticListener : ResourceWatcherDiagnosticListenerBase
    {
        protected readonly TelemetryClient Client;
        protected readonly TelemetryConfiguration Configuration;

        private const string _type = "ProcessStep";

        public ResourceWatcherDiagnosticListener(TelemetryConfiguration configuration)
        {
            this.Configuration = configuration;
            this.Client = new TelemetryClient(configuration);
            this.Client.InstrumentationKey = configuration.InstrumentationKey;
        }

        #region Event
        [DiagnosticName("Ark.Tools.ResourceWatcher.HostStartEvent")]
        public override void OnHostStartEvent()
        {
            var telemetry = new EventTelemetry
            {
                Name = "Ark.Tools.ResourceWatcher.HostStartEvent",
            };
            
            this.Client.TrackEvent(telemetry);
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.RunTookTooLong")]
        public override void RunTookTooLong(string tenant, Activity activity)
        {
            var telemetry = new EventTelemetry
            {
                Name = activity.OperationName,
            };

            // properly fill dependency telemetry operation context
            telemetry.Context.Operation.Id = activity.RootId;
            telemetry.Context.Operation.ParentId = activity.ParentId;
            telemetry.Timestamp = activity.StartTimeUtc;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Metrics.Add("ElapsedSeconds", activity.Duration.TotalSeconds);
            telemetry.Metrics.Add("ElapsedMinutes", activity.Duration.Minutes);

            this.Client.TrackEvent(telemetry);
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResourceTookTooLong")]
        public override void OnProcessResourceTookTooLong(string tenant, string resourceId, Activity activity)
        {
            var telemetry = new EventTelemetry
            {
                Name = activity.OperationName,   
            };

            // properly fill dependency telemetry operation context
            telemetry.Context.Operation.Id = activity.RootId;
            telemetry.Context.Operation.ParentId = activity.ParentId;
            telemetry.Timestamp = activity.StartTimeUtc;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Properties.Add("ResourceId", resourceId);
            telemetry.Metrics.Add("ElapsedSeconds", activity.Duration.TotalSeconds);
            telemetry.Metrics.Add("ElapsedMinutes", activity.Duration.Minutes);

            this.Client.TrackEvent(telemetry);
        }
        #endregion

        #region Exception
        [DiagnosticName("Ark.Tools.ResourceWatcher.ThrowDuplicateResourceIdRetrived")]
        public override void OnDuplicateResourceIdRetrived(string tenant, Exception exception)
        {
            var telemetryException = new ExceptionTelemetry
            {
                Exception = exception,
                Message = exception.Message
            };

            telemetryException.Properties.Add("Tenant", tenant);

            this.Client.TrackException(telemetryException);
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ReportRunConsecutiveFailureLimitReached")]
        public override void OnReportRunConsecutiveFailureLimitReached(string tenant, Exception exception)
        {
            var telemetryException = new ExceptionTelemetry
            {
                Exception = exception,
                Message = exception.Message
            };

            telemetryException.Properties.Add("Tenant", tenant);

            this.Client.TrackException(telemetryException);
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResourceSaveFailed")]
        public override void OnProcessResourceSaveFailed(string resourceId, string tenant, Exception exception)
        {
            var telemetryException = new ExceptionTelemetry
            {
                Exception = exception,
                Message = exception.Message
            };

            telemetryException.Properties.Add("Tenant", tenant);

            this.Client.TrackException(telemetryException);
        }
        #endregion

        #region Run
        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Stop")]
        public override void OnRunStop(   int resourcesFound
                                        , int normal
                                        , int noNewData
                                        , int noAction
                                        , int error
                                        , int skipped
                                        , string tenant
                                        , Exception exception)
        {
            Activity currentActivity = Activity.Current;

            var telemetry = new RequestTelemetry
            {
                Id = currentActivity.Id,
                Duration = currentActivity.Duration,
                Name = currentActivity.OperationName,
                Success = exception == null ? true : false,
                Timestamp = currentActivity.StartTimeUtc,
            };

            //Telemetry operation context
            telemetry.Context.Operation.Id = currentActivity.RootId;
            telemetry.Context.Operation.ParentId = currentActivity.ParentId;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Metrics.Add("ResourcesFound", resourcesFound);
            telemetry.Metrics.Add("Result_Normal", normal);
            telemetry.Metrics.Add("Result_NoNewData", noNewData);
            telemetry.Metrics.Add("Result_NoAction", noAction);
            telemetry.Metrics.Add("Result_Error", error);
            telemetry.Metrics.Add("Result_Skipped", skipped);

            //Exception
            if (exception != null)
            {
                var telemetryException = new ExceptionTelemetry
                {
                    Exception = exception,
                    Message = exception.Message
                };

                telemetryException.Properties.Add("Tenant", tenant);
                telemetryException.Metrics.Add("ResourcesFound", resourcesFound);
                telemetryException.Metrics.Add("Result_Normal", normal);
                telemetryException.Metrics.Add("Result_NoNewData", noNewData);
                telemetryException.Metrics.Add("Result_NoAction", noAction);
                telemetryException.Metrics.Add("Result_Error", error);
                telemetryException.Metrics.Add("Result_Skipped", skipped);

                this.Client.TrackException(telemetryException);
            }

            this.Client.TrackRequest(telemetry);
        }
        #endregion 

        #region GetResources
        [DiagnosticName("Ark.Tools.ResourceWatcher.GetResources.Stop")]
        public override void OnGetResourcesStop(int resourcesFound, TimeSpan elapsed, string tenant, Exception exception)
        {
            Activity currentActivity = Activity.Current;

            var telemetry = new DependencyTelemetry
            {
                Id = currentActivity.Id,
                Duration = currentActivity.Duration,
                Name = currentActivity.OperationName,
                Success = exception == null ? true : false,
                Timestamp = currentActivity.StartTimeUtc,
                Type = _type
            };

            //Telemetry operation context
            telemetry.Context.Operation.Id = currentActivity.RootId;
            telemetry.Context.Operation.ParentId = currentActivity.ParentId;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Properties.Add("Elapsed", elapsed.ToString());
            telemetry.Metrics.Add("ResourcesFound", resourcesFound);

            //Exception
            if (exception != null)
            {
                var telemetryException = new ExceptionTelemetry
                {
                    Exception = exception,
                    Message = exception.Message
                };

                telemetryException.Properties.Add("Tenant", tenant);
                telemetryException.Metrics.Add("ResourcesFound", resourcesFound);

                this.Client.TrackException(telemetryException);
            }

            this.Client.TrackDependency(telemetry);
        }
        #endregion

        #region CheckState

        [DiagnosticName("Ark.Tools.ResourceWatcher.CheckState.Stop")]
        public override void OnCheckStateStop(    int resourcesNew
                                                , int resourcesUpdated
                                                , int resourcesRetried
                                                , int resourcesRetriedAfterBan
                                                , int resourcesBanned
                                                , int resourcesNothingToDo
                                                , string tenant
                                                , Exception exception)
        {
            Activity currentActivity = Activity.Current;

            var telemetry = new DependencyTelemetry
            {
                Id = currentActivity.Id,
                Duration = currentActivity.Duration,
                Name = currentActivity.OperationName,
                Success = exception == null ? true : false,
                Timestamp = currentActivity.StartTimeUtc,
                Type = _type
            };

            //Telemetry operation context
            telemetry.Context.Operation.Id = currentActivity.RootId;
            telemetry.Context.Operation.ParentId = currentActivity.ParentId;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Metrics.Add("Resources_New", resourcesNew);
            telemetry.Metrics.Add("Resources_Updated", resourcesUpdated);
            telemetry.Metrics.Add("Resources_Retried", resourcesRetried);
            telemetry.Metrics.Add("Resources_RetriedAfterBan", resourcesRetriedAfterBan);
            telemetry.Metrics.Add("Resources_Banned", resourcesBanned);
            telemetry.Metrics.Add("Resources_NothingToDo", resourcesNothingToDo);

            //Exception
            if (exception != null)
            {
                var telemetryException = new ExceptionTelemetry
                {
                    Exception = exception,
                    Message = exception.Message
                };

                telemetryException.Properties.Add("Tenant", tenant);

                this.Client.TrackException(telemetryException);
            }

            this.Client.TrackDependency(telemetry);
        }
        #endregion

        #region ProcessResource
        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResource.Stop")]
        public override void OnProcessResourceStop(string tenant, int idx, int total, ProcessContext processContext, bool isBanned, Exception exception)
        {
            Activity currentActivity = Activity.Current;

            var telemetry = new DependencyTelemetry
            {
                Id = currentActivity.Id,
                Duration = currentActivity.Duration,
                Name = currentActivity.OperationName,
                Success = exception == null ? true : false,
                Timestamp = currentActivity.StartTimeUtc,
                Type = _type
            };

            //Telemetry operation context
            telemetry.Context.Operation.Id = currentActivity.RootId;
            telemetry.Context.Operation.ParentId = currentActivity.ParentId;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Properties.Add("Idx/Total", idx.ToString() + "/" + total.ToString());
            _propertiesProcessResource(telemetry, processContext);

            //Exception
            if (exception != null)
            {
                var telemetryException = new ExceptionTelemetry
                {
                    Exception = exception,
                    Message = exception.Message
                };

                //Properties and metrics
                telemetryException.Properties.Add("Tenant", tenant);
                telemetryException.Properties.Add("Idx/Total", idx.ToString() + "/" + total.ToString());
                _propertiesProcessResource(telemetryException, processContext);

                this.Client.TrackException(telemetryException);
            }

            this.Client.TrackDependency(telemetry);
        }

        private void _propertiesProcessResource(ISupportProperties data, ProcessContext processDataContext)
        {
            data.Properties.Add("ResourceId", processDataContext.CurrentInfo.ResourceId);
            data.Properties.Add("ProcessType", processDataContext.ProcessType.ToString());
            data.Properties.Add("ResultType", processDataContext.ResultType.ToString());

            if (processDataContext.LastState != default)
            {
                data.Properties.Add("CheckSum_Old", processDataContext.LastState.CheckSum);
                data.Properties.Add("Modified_Old", processDataContext.LastState.Modified.ToString());
            }

            if (processDataContext.NewState != default)
            {
                data.Properties.Add("RetryCount", processDataContext.NewState.RetryCount.ToString());
                data.Properties.Add("RetrievedAt", processDataContext.NewState.ToString());
                data.Properties.Add("CheckSum", processDataContext.NewState.CheckSum);
                data.Properties.Add("Modified", processDataContext.NewState.Modified.ToString());

                string extensionsString = JsonConvert.SerializeObject(processDataContext.NewState.Extensions, ArkDefaultJsonSerializerSettings.Instance);
                data.Properties.Add("Extensions", extensionsString);
            }
        }
        #endregion
    }
}
