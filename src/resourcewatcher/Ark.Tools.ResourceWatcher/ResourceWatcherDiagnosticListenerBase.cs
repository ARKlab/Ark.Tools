// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DiagnosticAdapter;

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ark.Tools.ResourceWatcher
{
    public abstract class ResourceWatcherDiagnosticListenerBase : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly List<IDisposable> _subscription = new();

        protected ResourceWatcherDiagnosticListenerBase()
        {
            this._subscription.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }


        void IObserver<DiagnosticListener>.OnCompleted()
        {

        }

        void IObserver<DiagnosticListener>.OnError(Exception error)
        {

        }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener value)
#pragma warning restore CA1033 // Interface methods should be callable by child types
        {
            if (value.Name == "Ark.Tools.ResourceWatcher")
            {
                this._subscription.Add(value.SubscribeWithAdapter(this));
            }
        }

        #region Event
        [DiagnosticName("Ark.Tools.ResourceWatcher.HostStartEvent")]
        public virtual void OnHostStartEvent()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.RunTookTooLong")]
        public virtual void RunTookTooLong(string tenant, Activity activity)
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResourceTookTooLong")]
        public virtual void OnProcessResourceTookTooLong(string tenant, string resourceId, Activity activity)
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

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Start")]
        public virtual void OnRunStart(RunType runType)
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Stop")]
        public virtual void OnRunStop(int resourcesFound
                                        , int normal
                                        , int noPayload
                                        , int noAction
                                        , int error
                                        , int skipped
                                        , string tenant
                                        , Exception exception)
        {

        }
        #endregion 

        #region GetResources

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

        [DiagnosticName("Ark.Tools.ResourceWatcher.CheckState.Start")]
        public virtual void OnCheckStateStart()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.CheckState.Stop")]
        public virtual void OnCheckStateStop(int resourcesNew
                                                , int resourcesUpdated
                                                , int resourcesRetried
                                                , int resourcesRetriedAfterBan
                                                , int resourcesBanned
                                                , int resourcesNothingToDo
                                                , string tenant
                                                , Exception exception)
        {

        }
        #endregion

        #region FetchResource

        [DiagnosticName("Ark.Tools.ResourceWatcher.FetchResource.Start")]
        public virtual void OnFetchResourceStart()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.FetchResource.Stop")]
        public virtual void OnFetchResourceStop(string tenant, ProcessContext processContext, Exception exception)
        {

        }

        #endregion

        #region ProcessResource

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResource.Start")]
        public virtual void OnProcessResourceStart()
        {

        }

        [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResource.Stop")]
        public virtual void OnProcessResourceStop(string tenant, ProcessContext processContext, Exception exception)
        {

        }

        #endregion

        #region IDisposable Support
        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    foreach (var d in _subscription)
                        d.Dispose();
                }

                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
