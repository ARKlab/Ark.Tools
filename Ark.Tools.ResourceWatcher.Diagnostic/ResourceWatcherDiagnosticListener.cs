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

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Event.Start")]
        public virtual void OnRunEventStart(RunType runType, DateTime now)
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


            // Type & success?


            this.Client.TrackEvent(telemetry);
        }

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
            telemetry.Metrics.Add("Total Resources", totalResources);
            telemetry.Metrics.Add("New Resources", newResources);

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
                Type = "Child" //?????
            };

            //Telemetry operation context
            telemetry.Context.Operation.Id = currentActivity.RootId;
            telemetry.Context.Operation.ParentId = currentActivity.ParentId;

            //Properties and metrics
            telemetry.Properties.Add("Tenant", tenant);
            telemetry.Metrics.Add("Resources Found", resourcesFound);

            //Exception
            if (exception != null)
                telemetry.Properties.Add("Exception", exception.ToString());

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
        public virtual void OnCheckStateStop()
        {

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
        public virtual void OnProcessResourceStop()
        {

        }
        #endregion
    }
}
