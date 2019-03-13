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
    public abstract class DiagnosticListenerBase : IObserver<DiagnosticListener>//, IDisposable
    {
        //private readonly List<IDisposable> subscription = new List<IDisposable>();

        //public void Dispose()
        //{
        //    foreach (var sub in subscription)
        //    {
        //        sub.Dispose();
        //    }
        //}

        void IObserver<DiagnosticListener>.OnCompleted()
        {

        }

        void IObserver<DiagnosticListener>.OnError(Exception error)
        {

        }

        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener value)
        {

        }

        #region Event
        [DiagnosticName("Ark.Tools.ResourceWatcher.HostStartEvent")]
        public virtual void OnHostStartEvent()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.RunTookTooLong")]
        public virtual void RunTookTooLong(string tenant, TimeSpan elapsed)
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResourceTookTooLong")]
        public virtual void OnProcessResourceTookTooLong(string tenant, string resourceId, TimeSpan elapsed)
        {

        }
        #endregion

        #region Exception
        [DiagnosticName("Ark.Tools.ResourceWatcher.ThrowDuplicateResourceIdRetrived")]
        public virtual void OnDuplicateResourceIdRetrived(string tenant, Exception exception)
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ReportRunConsecutiveFailureLimitReached")]
        public virtual void OnReportRunConsecutiveFailureLimitReached(string tenant, Exception exception)
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResourceSaveFailed")]
        public virtual void OnProcessResourceSaveFailed(string resourceId, string tenant, Exception exception)
        {

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

        }
        #endregion
    }
}
