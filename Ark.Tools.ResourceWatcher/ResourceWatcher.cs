// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using Ark.Tools.Core;
using EnsureThat;
using Nito.AsyncEx;
using NLog;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher
{
    public abstract class ResourceWatcher<T> where T : IResourceState
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IResourceWatcherConfig _config;
        private readonly IStateProvider _stateProvider;
        private object _lock = new object { };
        private volatile bool _isStarted = false;
        private CancellationTokenSource _cts;
        private Task _task;
        private readonly ResourceWatcherDiagnosticSource _diagnosticSource;

        public ResourceWatcher(IResourceWatcherConfig config, IStateProvider stateProvider)
        {
            EnsureArg.IsNotNull(config);
            EnsureArg.IsNotNull(stateProvider);

            _config = config;
            _stateProvider = stateProvider;
            _diagnosticSource = new ResourceWatcherDiagnosticSource(config.Tenant);
        }

        public void Start()
        {
            _start();
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        private void _start()
        {
            lock (_lock)
            {
                if (_isStarted == true)
                    throw new InvalidOperationException("Watcher is already started");
                _onBeforeStart();
                _isStarted = true;
            }



            _cts = new CancellationTokenSource();
            _task = Task.Run(async () =>
            {
                try
                {
                    await _runAsync(_cts.Token);
                }
                catch (TaskCanceledException)
                {

                }
            }
            , _cts.Token).FailFastOnException();
        }

        protected virtual void _onBeforeStart()
        {
        }        

        public async Task RunOnce(CancellationToken ctk = default)
        {
            lock (_lock)
            {
                if (_isStarted)
                    throw new ApplicationException("Invalid use of RunOnce: the watcher has been started and is working in background or another RunOnce is running.");
                _onBeforeStart();
                _isStarted = true;
            }

            try {
                await _runOnce(ctk).ConfigureAwait(false);
            }
            finally
            {
                _isStarted = false;
            }
        }

        protected virtual async Task _runOnce(CancellationToken ctk = default)
        {
            var now = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();

            MappedDiagnosticsLogicalContext.Set("RequestID", Guid.NewGuid().ToString());
            _logger.Info("Check started for tenant {0} at {1}", _config.Tenant, now);
            try
            {
                var infos = await _getResourcesInfo(ctk).ConfigureAwait(false);

                var bad = infos.GroupBy(x => x.ResourceId).FirstOrDefault(x => x.Count() > 1);
                if (bad != null)
                    throw new InvalidOperationException($"Found multiple entries for ResouceId:{bad.Key}");




                if (_config.SkipResourcesOlderThanDays.HasValue)
                    infos = infos
                            .Where(x => x.Modified.Date > LocalDateTime.FromDateTime(now).Date.PlusDays(-(int)_config.SkipResourcesOlderThanDays.Value))
                            ;

                var list = infos.ToList();

                _logger.Info("Found {0} resources in {1}", list.Count, sw.Elapsed);

                // check which entries are new or have been modified.

                var states = _config.IgnoreState ? Enumerable.Empty<ResourceState>() : await _stateProvider.LoadStateAsync(_config.Tenant, list.Select(i => i.ResourceId).ToArray(), ctk).ConfigureAwait(false);

                var toProcess = list.GroupJoin(states, i => i.ResourceId, s => s.ResourceId, (i, s) => new { CurrentInfo = i, Match = s.SingleOrDefault() })
                    .Where(x => x.Match == null  // new resource                        
                        || (x.Match.RetryCount > 0 && x.Match.RetryCount <= _config.MaxRetries) // retry 
                        || (x.Match.RetryCount == 0 && x.CurrentInfo.Modified > x.Match.Modified) // new version
                        || (x.Match.RetryCount > _config.MaxRetries 
                            && x.CurrentInfo.Modified > x.Match.Modified
                            && x.Match.LastEvent + _config.BanDuration < SystemClock.Instance.GetCurrentInstant()) // BAN expired and new version                
                        )
                    .ToList();




                var parallelism = _config.DegreeOfParallelism;

                _logger.Info("Found {0} resources to process with parallelism {1}", list.Count, parallelism);
                using (SemaphoreSlim throttler = new SemaphoreSlim(initialCount: (int)parallelism))
                {
                    int total = toProcess.Count;
                    var tasks = toProcess.Select(async (x,i) =>
                    {
                        await throttler.WaitAsync(ctk).ConfigureAwait(false);
                        try
                        {
                            await _processEntry(i, total, x.CurrentInfo, x.Match, ctk).ConfigureAwait(false);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    });

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }

                _logger.Info("Check successful for tenant {0} in {1}", _config.Tenant, sw.Elapsed);
                if (sw.Elapsed > _config.RunDurationNotificationLimit)
                    _logger.Fatal("Check for tenant {0} took too much: {1}", _config.Tenant, sw.Elapsed);
            } catch(Exception ex)
            {
                _logger.Error(ex, "Check failed for tenant {0} in {1}", _config.Tenant, sw.Elapsed);
                throw;
            }
        }

        private async Task _runAsync(CancellationToken ctk = default)
        {
            await Task.Yield();

            int exConsecutiveCount = 0;

            while (!ctk.IsCancellationRequested)
            {
                //var activity = _diagnosticSource.RunStart("normal");

                try
                {
                    await _runOnce(ctk);
                    
                    exConsecutiveCount = 0;
                }
                catch (Exception ex)
                {
                    if (++exConsecutiveCount == 10)
                    {
                        _logger.Fatal(ex, "Failed 10 times consecutively");
                        throw;
                    }
                    //activity?.AddTag("error", "error");
                }
                //finally
                //{
                //    _diagnosticSource.RunSuccessful(activity, );
                //}

                ctk.ThrowIfCancellationRequested();

                _logger.Info("Going to sleep for {0}s", _config.SleepSeconds);
                await Task.Delay(_config.SleepSeconds * 1000, ctk);
            }
        }

        protected abstract Task<IEnumerable<IResourceMetadata>> _getResourcesInfo(CancellationToken ctk = default);
        protected abstract Task<T> _retrievePayload(IResourceMetadata info, IResourceTrackedState lastState, CancellationToken ctk = default);
        protected abstract Task _processResource(ChangedStateContext<T> context, CancellationToken ctk = default);

        private async Task _processEntry(int idx, int total, IResourceMetadata info, ResourceState lastState, CancellationToken ctk = default)
        {
            try
            {
                _logger.Info("({4}/{5}) Detected change on Resource.Id=\"{0}\", Resource.Modified={1}, OldState.Modified={2}, OldState.Retry={3}. Processing..."
                    , info.ResourceId
                    , info.Modified
                    , lastState?.Modified
                    , lastState?.RetryCount
                    , idx
                    , total
                    );

                var sw = Stopwatch.StartNew();

                AsyncLazy<T> payload = new AsyncLazy<T>(() => _retrievePayload(info, lastState, ctk));

                var state = new ResourceState()
                {
                    Tenant = _config.Tenant,
                    ResourceId = info.ResourceId,
                    Modified = lastState?.Modified ?? info.Modified, // we want to update modified only on success or Ban or first run
                    LastEvent = SystemClock.Instance.GetCurrentInstant(),
                    RetryCount = lastState?.RetryCount ?? 0,
                    CheckSum = lastState?.CheckSum,
                    RetrievedAt = lastState?.RetrievedAt,
                    Extensions = info.Extensions
                };
                
                try {
                    
                    await _processResource(new ChangedStateContext<T>(info, lastState, payload), ctk).ConfigureAwait(false);
                    
                    // if handlers retrived data, fetch the result to check the checksum
                    if (payload.IsStarted)
                    {
                        // if the retrievePayload task gone in exception but the _processResource did not ...
                        // here we care only if we have a payload to use
                        if (payload.Task.Status == TaskStatus.RanToCompletion)
                        {
                            var newState = await payload;

                            if (newState != null)
                            {
                                if (!string.IsNullOrWhiteSpace(newState.CheckSum) && state.CheckSum != newState.CheckSum)
                                    _logger.Info("Checksum changed on Resource.Id=\"{0}\" from \"{1}\" to \"{2}\"", state.ResourceId, state.CheckSum, newState.CheckSum);

                                state.CheckSum = newState.CheckSum;
                                state.RetrievedAt = newState.RetrievedAt;
                            }
                            else // no payload retrived, so no new state. Generally due to a same-checksum
                            {
                            }

                            state.Modified = info.Modified;
                            state.RetryCount = 0; // success
                        }
                    } else // for some reason, no action has been and payload has not been retrieved. We do not change the state
                    {

                    }
                }
                catch (Exception ex)
                {
                    state.LastException = ex;

                    LogLevel lvl = ++state.RetryCount == _config.MaxRetries ? LogLevel.Fatal : LogLevel.Warn;
                    _logger.Log(lvl, ex, "Error while processing ResourceId=\"{0}\"", info.ResourceId);

                    // if we're in BAN and we tried to exit BAN and we failed, update the Modified anw
                    if (state.RetryCount > _config.MaxRetries)
                        state.Modified = info.Modified;
                }
                
                await _stateProvider.SaveStateAsync(new[] { state }, ctk).ConfigureAwait(false);

                _logger.Info("({3}/{4}) ResourceId=\"{0}\" handled {2}successfully in {1}", state.ResourceId, sw.Elapsed, state.RetryCount == 0 ? "" : "not ", idx
                    , total);
                if (sw.Elapsed > _config.ResourceDurationNotificationLimit)
                    _logger.Fatal("Processing of ResourceId=\"{0}\" took too much: {1}", state.ResourceId, sw.Elapsed);

            } catch (Exception ex)
            {
                // chomp it, we'll retry this file next time, forever, fuckit
                _logger.Error(ex, "({4}/{5}) Error in processing the ResourceId=\"{0}\". The execution will be automatically retried.", info.ResourceId);
            }
        }
    }

    public interface IResourceWatcherConfig
    {
        string Tenant { get; }
        int SleepSeconds { get; }
        int MaxRetries { get; }
        uint DegreeOfParallelism { get; }
        uint? SkipResourcesOlderThanDays { get; }
        bool IgnoreState { get; }
        Duration BanDuration { get; }
        TimeSpan RunDurationNotificationLimit { get; }
        TimeSpan ResourceDurationNotificationLimit { get; }
    }
    
    public sealed class ChangedStateContext<T> where T : IResourceState
    {
        public ChangedStateContext(IResourceMetadata info, IResourceTrackedState lastState, AsyncLazy<T> payload)
        {
            EnsureArg.IsNotNull(info);
            EnsureArg.IsNotNull(payload);

            Info = info;
            Payload = payload;
            LastState = lastState;
        }

        public AsyncLazy<T> Payload { get; }
        public IResourceMetadata Info { get; }
        public IResourceTrackedState LastState { get; }
    }
}
