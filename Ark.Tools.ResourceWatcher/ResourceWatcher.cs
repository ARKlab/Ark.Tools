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
            _diagnosticSource = new ResourceWatcherDiagnosticSource(config.Tenant, _logger);
        }

        public void Start()
        {
            _start();
            _diagnosticSource.HostStartEvent();
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

            try
            {
                await _runOnce(RunType.Once, ctk).ConfigureAwait(false);
            }
            finally
            {
                _isStarted = false;
            }
        }

        private async Task _runAsync(CancellationToken ctk = default)
        {
            await Task.Yield();

            int exConsecutiveCount = 0;

            while (!ctk.IsCancellationRequested)
            {
                try
                {
                    await _runOnce(RunType.Normal, ctk);

                    exConsecutiveCount = 0;
                }
                catch (Exception ex)
                {
                    if (++exConsecutiveCount == 10)
                    {
                        _diagnosticSource.ReportRunConsecutiveFailureLimitReached(ex, exConsecutiveCount);
                        throw;
                    }
                }

                ctk.ThrowIfCancellationRequested();

                _logger.Info("Going to sleep for {0}s", _config.SleepSeconds);
                await Task.Delay(_config.SleepSeconds * 1000, ctk);
            }
        }

        protected virtual async Task _runOnce(RunType runType, CancellationToken ctk = default)
        {
            var now = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();

            MappedDiagnosticsLogicalContext.Set("RequestID", Guid.NewGuid().ToString());
            var activityRun = _diagnosticSource.RunStart(runType, now);

            try
            {
                //GetResources
                var activityResource = _diagnosticSource.GetResourcesStart();

                var infos = await _getResourcesInfo(ctk).ConfigureAwait(false);

                var bad = infos.GroupBy(x => x.ResourceId).FirstOrDefault(x => x.Count() > 1);
                if (bad != null)
                    _diagnosticSource.ThrowDuplicateResourceIdRetrived(bad.Key);

                if (_config.SkipResourcesOlderThanDays.HasValue)
                    infos = infos
                            .Where(x => x.Modified.Date > LocalDateTime.FromDateTime(now).Date.PlusDays(-(int)_config.SkipResourcesOlderThanDays.Value))
                            ;

                var list = infos.ToList();
                _diagnosticSource.GetResourcesSuccessful(activityResource, list.Count, sw.Elapsed);

                //Check State - check which entries are new or have been modified.
                var activityCheckState = _diagnosticSource.CheckStateStart();

                var states = _config.IgnoreState ? Enumerable.Empty<ResourceState>() : await _stateProvider.LoadStateAsync(_config.Tenant, list.Select(i => i.ResourceId).ToArray(), ctk).ConfigureAwait(false);

                var evaluated = _createEvalueteList(list, states).ToList();
                
                _diagnosticSource.CheckStateSuccessful(activityCheckState, evaluated);

                //Process
                _logger.Info($"Found {list.Count} resources to process with parallelism {_config.DegreeOfParallelism}");

                var tasks = evaluated.Parallel((int)_config.DegreeOfParallelism, async (i, x) =>
                    await _processEntry((int)i+1, evaluated.Count, x, ctk));

                await Task.WhenAll(tasks).ConfigureAwait(false);

                _diagnosticSource.RunSuccessful(activityRun, evaluated, sw.Elapsed);

                if (sw.Elapsed > _config.RunDurationNotificationLimit)
                    _diagnosticSource.RunTookTooLong(sw.Elapsed);
            }
            catch (Exception ex)
            {
                _diagnosticSource.RunFailed(activityRun, ex, sw.Elapsed);
                throw;
            }
        }

        private IEnumerable<ProcessContext> _createEvalueteList(List<IResourceMetadata> list, IEnumerable<ResourceState> states)
        {
            var ev = list.GroupJoin(states, i => i.ResourceId, s => s.ResourceId, (i, s) =>
            {
                var x = new ProcessContext { CurrentInfo = i, LastState = s.SingleOrDefault() };
                if (x.LastState == null)
                {
                    x.ProcessType = ProcessType.New;
                }
                else if (x.LastState.RetryCount == 0 && x.CurrentInfo.Modified > x.LastState.Modified)
                {
                    x.ProcessType = ProcessType.Updated;
                }
                else if (x.LastState.RetryCount > 0 && x.LastState.RetryCount <= _config.MaxRetries)
                {
                    x.ProcessType = ProcessType.Retry;
                }
                else if (x.LastState.RetryCount > _config.MaxRetries
                    && x.CurrentInfo.Modified > x.LastState.Modified
                    && x.LastState.LastEvent + _config.BanDuration < SystemClock.Instance.GetCurrentInstant()
                    // BAN expired and new version                
                    )
                {
                    x.ProcessType = ProcessType.RetryAfterBan;
                }
                else if (x.LastState.RetryCount > _config.MaxRetries
                    && x.CurrentInfo.Modified > x.LastState.Modified
                    && !(x.LastState.LastEvent + _config.BanDuration < SystemClock.Instance.GetCurrentInstant())
                    // BAN               
                    )
                {
                    x.ProcessType = ProcessType.Banned;
                }
                else
                    x.ProcessType = ProcessType.NothingToDo;
                return x;
            });

            return ev;
        }

        protected abstract Task<IEnumerable<IResourceMetadata>> _getResourcesInfo(CancellationToken ctk = default);
        protected abstract Task<T> _retrievePayload(IResourceMetadata info, IResourceTrackedState lastState, CancellationToken ctk = default);
        protected abstract Task _processResource(ChangedStateContext<T> context, CancellationToken ctk = default);

        private async Task _processEntry(int idx, int total, ProcessContext processContext, CancellationToken ctk = default)
        {
            var info = processContext.CurrentInfo;
            var lastState = processContext.LastState;
            var dataType = processContext.ProcessType;

            if (processContext.ProcessType == ProcessType.NothingToDo || processContext.ProcessType == ProcessType.Banned)
            {
                processContext.ResultType = ResultType.Skipped;
                _logger.Info($"({idx}/{total}) ResourceId=\"{processContext.CurrentInfo.ResourceId}\" Process Type is: {processContext.ProcessType} - Skipped");
                return;
            }

            try
            {
                var processActivity = _diagnosticSource.ProcessResourceStart(idx, total, processContext);

                AsyncLazy<T> payload = new AsyncLazy<T>(() => _retrievePayload(info, lastState, ctk));

                var state = processContext.NewState = new ResourceState()
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

                try
                {
                    processContext.ResultType = ResultType.Normal;
                    IResourceState newState = default;

                    await _processResource(new ChangedStateContext<T>(info, lastState, payload), ctk).ConfigureAwait(false);

                    // if handlers retrived data, fetch the result to check the checksum
                    if (payload.IsStarted)
                    {
                        // if the retrievePayload task gone in exception but the _processResource did not ...
                        // here we care only if we have a payload to use
                        if (payload.Task.Status == TaskStatus.RanToCompletion)
                        {
                            newState = await payload;

                            if (newState != null)
                            {
                                if (!string.IsNullOrWhiteSpace(newState.CheckSum) && state.CheckSum != newState.CheckSum)
                                    _logger.Info("Checksum changed on ResourceId=\"{0}\" from \"{1}\" to \"{2}\"", state.ResourceId, state.CheckSum, newState.CheckSum);

                                state.CheckSum = newState.CheckSum;
                                state.RetrievedAt = newState.RetrievedAt;

                                processContext.ResultType = ResultType.Normal;
                            }
                            else // no payload retrived, so no new state. Generally due to a same-checksum
                            {
                                processContext.ResultType = ResultType.NoPayload;
                            }

                            state.Extensions = info.Extensions;
                            state.Modified = info.Modified;
                            state.RetryCount = 0; // success
                        }
                        else
                        {
                            throw new NotSupportedException($"({idx}/{total}) ResourceId=\"{state.ResourceId}\" we cannot reach this point!");
                        }
                    }
                    else // for some reason, no action has been and payload has not been retrieved. We do not change the state
                    {
                        processContext.ResultType = ResultType.NoAction;
                    }

                    _diagnosticSource.ProcessResourceSuccessful(processActivity, idx, total, processContext);
                    if(processActivity.Duration > _config.ResourceDurationNotificationLimit)
                        _diagnosticSource.ProcessResourceTookTooLong(info.ResourceId, processActivity.Duration);
                }
                catch (Exception ex)
                {
                    state.LastException = ex;
                    processContext.ResultType = ResultType.Error;
                    var isBanned = ++state.RetryCount == _config.MaxRetries;

                    state.Extensions = info.Extensions;
                    state.Modified = info.Modified;

                    _diagnosticSource.ProcessResourceFailed(processActivity, idx, total, processContext, isBanned, ex);
                }

                await _stateProvider.SaveStateAsync(new[] { state }, ctk).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                // chomp it, we'll retry this file next time, forever, fuckit
                _diagnosticSource.ProcessResourceSaveFailed(info.ResourceId, ex);
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

    public enum RunType
    {
        Once,
        Normal
    }

    public enum ResultType
    {
        Normal,
        NoAction,
        NoPayload,
        Error,
        Skipped
    }

    public enum ProcessType
    {
        New,
        Updated,
        Retry,
        RetryAfterBan,
        Banned,
        NothingToDo
    }

    public class ProcessContext
    {
        public IResourceMetadata CurrentInfo { get; set; }
        public ResourceState LastState { get; set; }
        public ResourceState NewState { get; set; }
        public ProcessType ProcessType { get; set; }
        public ResultType ResultType { get; set; }
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
