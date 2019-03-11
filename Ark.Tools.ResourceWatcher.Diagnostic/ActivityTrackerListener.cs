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
    public class ActivityTrackerListener : IObserver<DiagnosticListener>, IDisposable
    {
        protected readonly TelemetryClient Client;
        protected readonly TelemetryConfiguration Configuration;

        private readonly List<IDisposable> subscription = new List<IDisposable>();

        public ActivityTrackerListener(TelemetryConfiguration configuration)
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

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run")]
        public virtual void OnRun()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Start")]
        public virtual void OnRunStart()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Stop")]
        public virtual void OnRunStart(bool payload, string entity)
        {
            Activity currentActivity = Activity.Current;

            var telemetry = new DependencyTelemetry
            {
                Id = currentActivity.Id,
                Duration = currentActivity.Duration,
                Name = currentActivity.OperationName
            };

            // properly fill dependency telemetry operation context
            telemetry.Context.Operation.Id = currentActivity.RootId;
            telemetry.Context.Operation.ParentId = currentActivity.ParentId;
            telemetry.Timestamp = currentActivity.StartTimeUtc;

            // Type & success?


            this.Client.TrackDependency(telemetry);
        }
    }
}
