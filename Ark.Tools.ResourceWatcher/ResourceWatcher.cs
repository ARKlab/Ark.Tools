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

                var toEvaluate = _createEvalueteList(list, states);

                var toProcess = toEvaluate.Where(w => 
                                w.ProcessDataType != ProcessDataType.NothingToDo
                                && w.ProcessDataType != ProcessDataType.Banned
                                )
                                .ToList();

                _diagnosticSource.CheckStateSuccessful(activityCheckState, toEvaluate);

                //Process
                _logger.Info($"Found {list.Count} resources to process with parallelism {_config.DegreeOfParallelism}");

                var tasks = toProcess.Parallel((int)_config.DegreeOfParallelism, async (i, x) =>
                    await _processEntry((int)i, toProcess.Count, x.CurrentInfo, x.Match, x.ProcessDataType, ctk));

                await Task.WhenAll(tasks).ConfigureAwait(false);

                _diagnosticSource.RunSuccessful(activityRun, toProcess.Count, sw.Elapsed);

                if (sw.Elapsed > _config.RunDurationNotificationLimit)
                    _diagnosticSource.RunTookTooLong(sw.Elapsed);
            }
            catch (Exception ex)
            {
                _diagnosticSource.RunFailed(activityRun, ex, sw.Elapsed);
                throw;
            }
        }

        private IEnumerable<ProcessData> _createEvalueteList(List<IResourceMetadata> list, IEnumerable<ResourceState> states)
        {
            var ev = list.GroupJoin(states, i => i.ResourceId, s => s.ResourceId, (i, s) =>
            {
                var x = new ProcessData { CurrentInfo = i, Match = s.SingleOrDefault() };
                if (x.Match == null)
                {
                    x.ProcessDataType = ProcessDataType.New;
                }
                else if (x.Match.RetryCount == 0 && x.CurrentInfo.Modified > x.Match.Modified)
                {
                    x.ProcessDataType = ProcessDataType.Updated;
                }
                else if (x.Match.RetryCount > 0 && x.Match.RetryCount <= _config.MaxRetries)
                {
                    x.ProcessDataType = ProcessDataType.Retry;
                }
                else if (x.Match.RetryCount > _config.MaxRetries
                    && x.CurrentInfo.Modified > x.Match.Modified
                    && x.Match.LastEvent + _config.BanDuration < SystemClock.Instance.GetCurrentInstant()
                    // BAN expired and new version                
                    )
                {
                    x.ProcessDataType = ProcessDataType.RetryAfterBan;
                }
                else if (x.Match.RetryCount > _config.MaxRetries
                    && x.CurrentInfo.Modified > x.Match.Modified
                    && !(x.Match.LastEvent + _config.BanDuration < SystemClock.Instance.GetCurrentInstant())
                    // BAN               
                    )
                {
                    x.ProcessDataType = ProcessDataType.Banned;
                }
                else
                    x.ProcessDataType = ProcessDataType.NothingToDo;
                return x;
            });

            return ev;
        }

        protected abstract Task<IEnumerable<IResourceMetadata>> _getResourcesInfo(CancellationToken ctk = default);
        protected abstract Task<T> _retrievePayload(IResourceMetadata info, IResourceTrackedState lastState, CancellationToken ctk = default);
        protected abstract Task _processResource(ChangedStateContext<T> context, CancellationToken ctk = default);

        private async Task _processEntry(int idx, int total, IResourceMetadata info, ResourceState lastState, ProcessDataType type, CancellationToken ctk = default)
        {
            try
            {
                var processActivity = _diagnosticSource.ProcessResourceStart(info, lastState);

                _logger.Info("({4}/{5}) Detected change on ResourceId=\"{0}\", Resource.Modified={1}, OldState.Modified={2}, OldState.Retry={3}. Processing..."
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

                try
                {
                    var processType = ProcessType.Normal;
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

                                processType = ProcessType.Normal;
                            }
                            else // no payload retrived, so no new state. Generally due to a same-checksum
                            {
                                processType = ProcessType.NoPayload;
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
                        processType = ProcessType.NoAction;
                    }

                    if (sw.Elapsed > _config.ResourceDurationNotificationLimit)
                        _diagnosticSource.ProcessResourceTookTooLong(info.ResourceId, sw.Elapsed);

                    _diagnosticSource.ProcessResourceSuccessful(processActivity
                                                                , idx
                                                                , info.ResourceId
                                                                , sw.Elapsed
                                                                , state.RetryCount
                                                                , total
                                                                , newState
                                                                , type
                                                                , processType);
                }
                catch (Exception ex)
                {
                    state.LastException = ex;

                    LogLevel lvl = ++state.RetryCount == _config.MaxRetries ? LogLevel.Fatal : LogLevel.Warn;
                    _diagnosticSource.ProcessResourceFailed(processActivity, lvl, info.ResourceId, type, ProcessType.Error, ex);

                    state.Extensions = info.Extensions;
                    state.Modified = info.Modified;
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

    public enum ProcessType
    {
        Normal,
        NoAction,
        NoPayload,
        Error,
        ErrorAndRetry
    }

    public enum ProcessDataType
    {
        New,
        Updated,
        Retry,
        RetryAfterBan,
        Banned,
        NothingToDo
    }

    public class ProcessData
    {
        public IResourceMetadata CurrentInfo { get; set; }
        public ResourceState Match { get; set; }
        public ProcessDataType ProcessDataType { get; set; }
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
