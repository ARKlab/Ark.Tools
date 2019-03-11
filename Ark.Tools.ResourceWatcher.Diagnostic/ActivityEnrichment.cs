using Microsoft.Extensions.DiagnosticAdapter;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public class ActivityEnrichment : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly List<IDisposable> subscription = new List<IDisposable>();
        private readonly ActivityEnrichmentOptions options;

        public ActivityEnrichment(IOptions<ActivityEnrichmentOptions> options)
        {
            this.options = options.Value;
            this.subscription.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        //[DiagnosticName("System.Net.Http.HttpRequestOut.Start")]
        //public virtual void OnHttpRequestOutStart(System.Net.Http.HttpRequestMessage request)
        //{
        //    if (this.options.HostNames.Contains(request.RequestUri.Host))
        //    {
        //        foreach (var header in this.options.Headers)
        //        {
        //            if (request.Headers.TryGetValues(header, out var values))
        //            {
        //                Activity.Current.AddTag($"x-request-header-{header}", string.Join(", ", values));
        //            }
        //        }


        //    }
        //}

        //[DiagnosticName("System.Net.Http.HttpRequestOut.Stop")]
        //public virtual void OnHttpRequestOutStop(System.Net.Http.HttpRequestMessage request, System.Net.Http.HttpResponseMessage response, TaskStatus requestTaskStatus)
        //{
        //    if (this.options.HostNames.Contains(request.RequestUri.Host))
        //    {
        //        foreach (var header in this.options.Headers)
        //        {
        //            if (response.Headers.TryGetValues(header, out var values))
        //            {
        //                Activity.Current.AddTag($"x-response-header-{header}", string.Join(", ", values));
        //            }
        //        }

        //    }
        //}

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
            if (value.Name == "ResourceWatcherDiagnosticSource")
            {
                this.subscription.Add(value.SubscribeWithAdapter(this));
            }
        }
    }
}
