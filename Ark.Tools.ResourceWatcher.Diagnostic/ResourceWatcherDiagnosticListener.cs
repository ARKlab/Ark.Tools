using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Common;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DiagnosticAdapter;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{ 
    public class ResourceWatcherDiagnosticListener : IObserver<DiagnosticListener>, IDisposable
    {
        protected readonly TelemetryClient Client;
        protected readonly TelemetryConfiguration Configuration;

        private readonly List<IDisposable> subscription = new List<IDisposable>();
        private const string _type = "ProcessStep";

        public ResourceWatcherDiagnosticListener(TelemetryConfiguration configuration)
        {
            this.Configuration = configuration;
            this.Client = new TelemetryClient(configuration);

            this.subscription.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        public void Dispose()
        {
            foreach (var sub in subscription)
            {
                sub.Dispose();
            }
        }

        void IObserver<DiagnosticListener>.OnCompleted()
        {

        }

        void IObserver<DiagnosticListener>.OnError(Exception error)
        {

        }

        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener value)
        {
            if (value.Name == "Ark.Tools.ResourceWatcher")
            {
                this.subscription.Add(value.SubscribeWithAdapter(this));
            }
        }

        #region Event
        [DiagnosticName("Ark.Tools.ResourceWatcher.HostStartEvent")]
        public virtual void OnHostStartEvent()
        {
            var telemetry = new EventTelemetry
            {
                Name = "Ark.Tools.ResourceWatcher.HostStartEvent",
            };
            
            this.Client.TrackEvent(telemetry);
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.RunTookTooLong")]
        public virtual void RunTookTooLong(string tenant, TimeSpan elapsed)
        {
            Activity currentActivity = Activity.Current;

            var telemetry = new EventTelemetry
            {
                Name = currentActivity.OperationName,
            };

            // properly fill dependency telemetry operation context
            telemetry.Context.Operation.Id = currentActivity.RootId;
            telemetry.Context.Operation.ParentId = currentActivity.ParentId;
            telemetry.Timestamp = currentActivity.StartTimeUtc;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Metrics.Add("ElapsedSeconds", elapsed.TotalSeconds);

            this.Client.TrackEvent(telemetry);
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResourceTookTooLong")]
        public virtual void OnProcessResourceTookTooLong(string tenant, string resourceId, TimeSpan elapsed)
        {
            Activity currentActivity = Activity.Current;

            var telemetry = new EventTelemetry
            {
                Name = currentActivity.OperationName,   
            };

            // properly fill dependency telemetry operation context
            telemetry.Context.Operation.Id = currentActivity.RootId;
            telemetry.Context.Operation.ParentId = currentActivity.ParentId;
            telemetry.Timestamp = currentActivity.StartTimeUtc;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Properties.Add("ResourceId", resourceId);
            telemetry.Metrics.Add("ElapsedSeconds", elapsed.TotalSeconds);

            this.Client.TrackEvent(telemetry);
        }
        #endregion

        #region Exception
        [DiagnosticName("Ark.Tools.ResourceWatcher.ThrowDuplicateResourceIdRetrived")]
        public virtual void OnDuplicateResourceIdRetrived(string tenant, Exception exception)
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
        public virtual void OnReportRunConsecutiveFailureLimitReached(string tenant, Exception exception)
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
        public virtual void OnProcessResourceSaveFailed(string resourceId, string tenant, Exception exception)
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
        [DiagnosticName("Ark.Tools.ResourceWatcher.Run")]
        public virtual void OnRun()
        {
        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Start")]
        public virtual void OnRunStart(RunType runType, DateTime now)
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Stop")]
        public virtual void OnRunStop(int totalResources, int newResources, string tenant, Exception exception)
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
            telemetry.Metrics.Add("TotalResources", totalResources);
            telemetry.Metrics.Add("NewResources", newResources);

            //Exception
            if (exception != null)
                telemetry.Properties.Add("Exception", exception.ToString());

            this.Client.TrackRequest(telemetry);
        }
        #endregion 

        #region GetResources
        [DiagnosticName("Ark.Tools.ResourceWatcher.GetResources")]
        public virtual void OnGetResources()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.GetResources.Start")]
        public virtual void OnGetResourcesStart()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.GetResources.Stop")]
        public virtual void OnGetResourcesStop(int resourcesFound, TimeSpan elapsed, string tenant, Exception exception)
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
        [DiagnosticName("Ark.Tools.ResourceWatcher.CheckState")]
        public virtual void OnCheckState()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.CheckState.Start")]
        public virtual void OnCheckStateStart()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.CheckState.Stop")]
        public virtual void OnCheckStateStop(     int resourcesNew
                                                , int resourcesUpdated
                                                , int resourcesRetried
                                                , int resourcesRetriedAfterBan
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
            telemetry.Metrics.Add("ResourcesNew", resourcesNew);
            telemetry.Metrics.Add("ResourcesUpdated", resourcesUpdated);
            telemetry.Metrics.Add("ResourcesRetried", resourcesRetried);
            telemetry.Metrics.Add("ResourcesRetriedAfterBan", resourcesRetriedAfterBan);

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
        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResource")]
        public virtual void OnProcessResource()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResource.Start")]
        public virtual void OnProcessResourceStart()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResource.Stop")]
        public virtual void OnProcessResourceStop(string resourceId, ProcessDataType processDataType, IResourceState state, string tenant, Exception exception)
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
            telemetry.Properties.Add("ResourceId", resourceId);
            telemetry.Properties.Add("ProcessDataType", processDataType.ToString());

            if (state != default)
            {
                telemetry.Properties.Add("RetrievedAt", state.RetrievedAt.ToString());
                telemetry.Properties.Add("CheckSum", state.CheckSum);
            }

            //Exception
            if (exception != null)
            {
                var telemetryException = new ExceptionTelemetry
                {
                    Exception = exception,
                    Message = exception.Message
                };

                telemetryException.Properties.Add("Tenant", tenant);
                telemetryException.Properties.Add("ResourceId", resourceId);
                
                this.Client.TrackException(telemetryException);
            }

            this.Client.TrackDependency(telemetry);
        }
        #endregion
    }
}
