// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;

using NLog;

using NodaTime;

using System.Diagnostics;
using System.Globalization;

namespace Ark.Tools.ResourceWatcher;

public abstract class ResourceWatcher<T> : IDisposable where T : IResourceState
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly IResourceWatcherConfig _config;
    private readonly IStateProvider _stateProvider;
    private readonly Lock _lock = new() { };
    private volatile bool _isStarted;
    private CancellationTokenSource? _cts;
    private Task? _task;
    private readonly ResourceWatcherDiagnosticSource _diagnosticSource;

    protected ResourceWatcher(IResourceWatcherConfig config, IStateProvider stateProvider)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(stateProvider);

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
        _cts?.Cancel();
    }

    private void _start()
    {
        lock (_lock)
        {
            InvalidOperationException.ThrowIf(_isStarted == true, "Watcher is already started");
            _onBeforeStart();
            _isStarted = true;
        }

        _cts = new CancellationTokenSource();
        _task = Task.Run(async () =>
        {
            try
            {
                await _runAsync(_cts.Token).ConfigureAwait(false);
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
            InvalidOperationException.ThrowIf(_isStarted, "Invalid use of RunOnce: the watcher has been started and is working in background or another RunOnce is running.");
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
                await _runOnce(RunType.Normal, ctk).ConfigureAwait(false);

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

            _logger.Info(CultureInfo.InvariantCulture, "Going to sleep for {SleepSeconds}s", _config.SleepSeconds);
            await Task.Delay(_config.SleepSeconds * 1000, ctk).ConfigureAwait(false);
        }
    }

    protected virtual async Task _runOnce(RunType runType, CancellationToken ctk = default)
    {
        var now = DateTime.UtcNow;

        using var activityRun = _diagnosticSource.RunStart(runType, now);

        try
        {
            //GetResources
            var list = await _getResources(now, ctk).ConfigureAwait(false);

            //Check State - check which entries are new or have been modified.
            var evaluated = await _evaluateActions(list, ctk).ConfigureAwait(false);

            //Process
            var toProcess = evaluated.Where(x => !x.ResultType.HasValue).ToList();
            var skipped = evaluated.Count - toProcess.Count;

            _logger.Info(CultureInfo.InvariantCulture, "Found {SkippedCount} resources to skip", skipped);
            _logger.Info(CultureInfo.InvariantCulture, "Found {ToProcessCount} resources to process with parallelism {DegreeOfParallelism}", toProcess.Count, _config.DegreeOfParallelism);

            // Unlink the _processEntry Span from the parent run to avoid RootId being too big
            // This is mainly an hack for Application Insights but in general we want to avoid too big RootId with 100s of 1000s Spans
            Activity.Current = null;
            try
            {
                var count = toProcess.Count;
                await toProcess.Parallel((int)_config.DegreeOfParallelism, (i, x, ct) =>
                {
                    x.Total = count;
                    x.Index = i + 1;
                    return _processEntry(x, ct);
                }, ctk).ConfigureAwait(false);
            }
            finally
            {
                Activity.Current = activityRun;
            }

            _diagnosticSource.RunSuccessful(activityRun, evaluated);

            if (activityRun.Duration > _config.RunDurationNotificationLimit)
                _diagnosticSource.RunTookTooLong(activityRun);
        }
        catch (Exception ex)
        {
            _diagnosticSource.RunFailed(activityRun, ex);
            throw;
        }
    }

    private async Task<IList<ProcessContext>> _evaluateActions(IList<IResourceMetadata> list, CancellationToken ctk)
    {
        using var activityCheckState = _diagnosticSource.CheckStateStart();
        try
        {
            var states = _config.IgnoreState ? Enumerable.Empty<ResourceState>() : await _stateProvider.LoadStateAsync(_config.Tenant, list.Select(i => i.ResourceId).ToArray(), ctk).ConfigureAwait(false);

            var evaluated = _createEvalueteList(list, states);

            _diagnosticSource.CheckStateSuccessful(activityCheckState, evaluated);

            return evaluated;
        }
        catch (Exception ex)
        {
            _diagnosticSource.CheckStateFailed(activityCheckState, ex);
            throw;
        }
    }

    private async Task<IList<IResourceMetadata>> _getResources(DateTime now, CancellationToken ctk)
    {
        using var activityResource = _diagnosticSource.GetResourcesStart();

        try
        {
            var infos = await _getResourcesInfo(ctk).ConfigureAwait(false);

            var bad = infos.GroupBy(x => x.ResourceId, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
            if (bad != null)
                _diagnosticSource.ThrowDuplicateResourceIdRetrived(bad.Key);

            if (_config.SkipResourcesOlderThanDays.HasValue)
                infos = infos
                        .Where(x => _getEarliestModified(x).Date > LocalDateTime.FromDateTime(now).Date.PlusDays(-(int)_config.SkipResourcesOlderThanDays.Value))
                        ;

            var list = infos.ToList();

            _diagnosticSource.GetResourcesSuccessful(activityResource, list.Count);
            return list;
        }
        catch (Exception ex)
        {
            _diagnosticSource.GetResourcesFailed(activityResource, ex);
            throw;
        }
    }

    private IList<ProcessContext> _createEvalueteList(IList<IResourceMetadata> list, IEnumerable<ResourceState> states)
    {
        var ev = list.GroupJoin(states, i => i.ResourceId, s => s.ResourceId, (i, s) =>
         {
             var x = new ProcessContext(i) { LastState = s.SingleOrDefault() };
             if (x.LastState == null)
             {
                 x.ProcessType = ProcessType.New;
             }
             else if (x.LastState.RetryCount == 0 && x.IsResourceUpdated(out _))
             {
                 x.ProcessType = ProcessType.Updated;
             }
             else if (x.LastState.RetryCount > 0 && x.LastState.RetryCount <= _config.MaxRetries)
             {
                 x.ProcessType = ProcessType.Retry;
             }
             else if (x.LastState.RetryCount > _config.MaxRetries
                 && x.IsResourceUpdated(out _)
                 && x.LastState.LastEvent + _config.BanDuration < SystemClock.Instance.GetCurrentInstant()
                 // BAN expired and new version                
                 )
             {
                 x.ProcessType = ProcessType.RetryAfterBan;
             }
             else if (x.LastState.RetryCount > _config.MaxRetries
                 && x.IsResourceUpdated(out _)
                 && !(x.LastState.LastEvent + _config.BanDuration < SystemClock.Instance.GetCurrentInstant())
                 // BAN               
                 )
             {
                 x.ProcessType = ProcessType.Banned;
             }
             else
                 x.ProcessType = ProcessType.NothingToDo;

             if (x.ProcessType == ProcessType.NothingToDo || x.ProcessType == ProcessType.Banned)
             {
                 x.ResultType = ResultType.Skipped;
             }

             return x;
         }, StringComparer.Ordinal).ToList();

        return ev;
    }

    protected abstract Task<IEnumerable<IResourceMetadata>> _getResourcesInfo(CancellationToken ctk = default);
    protected abstract Task<T?> _retrievePayload(IResourceMetadata info, IResourceTrackedState? lastState, CancellationToken ctk = default);
    protected abstract Task _processResource(ChangedStateContext<T> context, CancellationToken ctk = default);

    private async Task<T?> _fetchResource(ProcessContext pc, CancellationToken ctk = default)
    {
        var info = pc.CurrentInfo;
        var lastState = pc.LastState;

        using var activity = _diagnosticSource.FetchResourceStart(pc);

        try
        {
            var res = await _retrievePayload(info, lastState, ctk).ConfigureAwait(false);
            _diagnosticSource.FetchResourceSuccessful(activity, pc);
            return res;
        }
        catch (Exception ex)
        {
            _diagnosticSource.FetchResourceFailed(activity, pc, ex);
            throw;
        }
    }

    private async Task _processEntry(ProcessContext pc, CancellationToken ctk = default)
    {
        var info = pc.CurrentInfo;
        var lastState = pc.LastState;
        var dataType = pc.ProcessType;
        using var scope = ScopeContext.PushNestedStateProperties(info.ResourceId,
        [
            // when 'logging' these, 'info' is serialized as the actual implementation which may contain a lot of data. slice to 'log' only the Interface
            new KeyValuePair<string, object?>("currentInfo", new { info.ResourceId, info.Modified, info.ModifiedSources, info.Extensions }),
            new KeyValuePair<string, object?>("lastState", lastState)
        ]);
        try
        {
            using var processActivity = _diagnosticSource.ProcessResourceStart(pc);

            var payload = new AsyncLazy<T?>(() => _fetchResource(pc, ctk));

            var state = pc.NewState = new ResourceState()
            {
                Tenant = _config.Tenant,
                ResourceId = info.ResourceId,
                Modified = lastState?.Modified ?? default, // we want to update modified only on success so that can be used as 'Last successful Modified' by Process logic
                ModifiedSources = lastState?.ModifiedSources,
                LastEvent = SystemClock.Instance.GetCurrentInstant(),
                RetryCount = lastState?.RetryCount ?? 0,
                CheckSum = lastState?.CheckSum,
                RetrievedAt = lastState?.RetrievedAt,
                Extensions = info.Extensions
            };

            try
            {
                pc.ResultType = ResultType.Normal;
                IResourceState? newState = default;

                await _processResource(new ChangedStateContext<T>(info, lastState, payload), ctk).ConfigureAwait(false);

                // if handlers retrived data, fetch the result to check the checksum
                if (payload.IsStarted)
                {
                    // if the retrievePayload task gone in exception but the _processResource did not ...
                    // here we care only if we have a payload to use
                    if (payload.Task.Status == TaskStatus.RanToCompletion)
                    {
                        newState = await payload.ConfigureAwait(false);

                        if (newState != null)
                        {
                            if (!string.IsNullOrWhiteSpace(newState.CheckSum) && state.CheckSum != newState.CheckSum)
                                _logger.Info(CultureInfo.InvariantCulture, "Checksum changed on ResourceId={ResourceId} from {OldChecksum} to {NewChecksum}", state.ResourceId, state.CheckSum, newState.CheckSum);

                            state.CheckSum = newState.CheckSum;
                            state.RetrievedAt = newState.RetrievedAt;

                            pc.ResultType = ResultType.Normal;
                        }
                        else // no payload retrived, so no new state. Generally due to a same-checksum
                        {
                            pc.ResultType = ResultType.NoNewData;
                        }

                        state.Extensions = info.Extensions;
                        state.Modified = info.Modified;
                        state.ModifiedSources = info.ModifiedSources;
                        state.RetryCount = 0; // success
                    }
                    else
                    {
                        throw new NotSupportedException($"({pc.Index}/{pc.Total}) ResourceId=\"{state.ResourceId}\" we cannot reach this point!");
                    }
                }
                else // for some reason, no action has been and payload has not been retrieved. We do not change the state
                {
                    pc.ResultType = ResultType.NoAction;
                }

                _diagnosticSource.ProcessResourceSuccessful(processActivity, pc);

                if (processActivity.Duration > _config.ResourceDurationNotificationLimit)
                    _diagnosticSource.ProcessResourceTookTooLong(info.ResourceId, processActivity);
            }
            catch (Exception ex)
            {
                state.LastException = ex;
                pc.ResultType = ResultType.Error;
                var isBanned = ++state.RetryCount == _config.MaxRetries;

                state.Extensions = info.Extensions;

                _diagnosticSource.ProcessResourceFailed(processActivity, pc, isBanned, ex);
            }

            await _stateProvider.SaveStateAsync([state], ctk).ConfigureAwait(false);

        }
        catch (Exception ex)
        {
            // chomp it, we'll retry this file next time, forever, fuckit
            pc.ResultType = ResultType.Error;
            _diagnosticSource.ProcessResourceSaveFailed(info.ResourceId, ex);
        }
    }

    private static LocalDateTime _getEarliestModified(IResourceMetadata info)
    {
        if (info.ModifiedSources != null && info.ModifiedSources.Count != 0)
        {
            return info.ModifiedSources.Max(x => x.Value);
        }
        else
        {
            return info.Modified;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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
    NoNewData,
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
    public ProcessContext(IResourceMetadata currentInfo)
    {
        CurrentInfo = currentInfo;
    }

    public IResourceMetadata CurrentInfo { get; }
    public ResourceState? LastState { get; set; }
    public ResourceState? NewState { get; set; }
    public ProcessType ProcessType { get; set; }
    public ResultType? ResultType { get; set; }
    public int? Index { get; set; }
    public int? Total { get; set; }

    public bool IsResourceUpdated(out (string? source, LocalDateTime? current, LocalDateTime? last) changed)
    {
        if (CurrentInfo.ModifiedSources != null && CurrentInfo.ModifiedSources.Count != 0)
        {
            if (LastState?.ModifiedSources != null && LastState.ModifiedSources.Count != 0)
            {
                if (CurrentInfo.ModifiedSources.Any(x => !LastState.ModifiedSources.ContainsKey(x.Key)))
                {
                    //New State contains new sources modified for the resource
                    var firstNewSource = CurrentInfo.ModifiedSources.First(x => !LastState.ModifiedSources.ContainsKey(x.Key));
                    changed = (
                                    source: firstNewSource.Key,
                                    current: firstNewSource.Value,
                                    last: null
                                );

                    return true;
                }
                else if (CurrentInfo.ModifiedSources.Any(x => x.Value > LastState.ModifiedSources[x.Key]))
                {
                    //One or more sources have an updated modified respect the corrisponding source into last state ModifiedSources
                    var firstUpdatedSource = CurrentInfo.ModifiedSources.First(x => x.Value > LastState.ModifiedSources[x.Key]);
                    changed = (
                                    source: firstUpdatedSource.Key,
                                    current: firstUpdatedSource.Value,
                                    last: LastState.ModifiedSources[firstUpdatedSource.Key]
                                );

                    return true;
                }
                else
                {
                    changed = default;
                    return false;
                }
            }
            else if (LastState?.Modified != null && LastState.Modified != default)
            {
                if (CurrentInfo.ModifiedSources.Any(x => x.Value > LastState.Modified))
                {
                    //One or more sources have an updated modify respect to Modified
                    var firstUpdatedSource = CurrentInfo.ModifiedSources.First(x => x.Value > LastState.Modified);
                    changed = (
                                    source: firstUpdatedSource.Key,
                                    current: firstUpdatedSource.Value,
                                    last: LastState.Modified
                                );

                    return true;
                }
                else
                {
                    changed = default;
                    return false;
                }
            }
            else if (LastState == null)
            {
                //new resource
                changed = (
                                source: CurrentInfo.ModifiedSources.First().Key,
                                current: CurrentInfo.ModifiedSources.First().Value,
                                last: null
                            );

                return true;
            }
            else
            {
                changed = default;
                return false;
            }
        }
        else if (CurrentInfo.Modified != default)
        {
            if (LastState?.ModifiedSources != null && LastState.ModifiedSources.Count != 0)
            {
                if (LastState.ModifiedSources.Any(x => x.Value < CurrentInfo.Modified))
                {
                    //the new single modified is major at least of one old ModifiedSources
                    changed = (
                                    source: null,
                                    current: CurrentInfo.Modified,
                                    last: LastState.ModifiedSources.Max(x => x.Value)
                                );
                    return true;
                }
                else
                {
                    changed = default;
                    return false;
                }
            }
            else if (LastState != null && LastState.Modified != default)
            {
                if (CurrentInfo.Modified > LastState.Modified)
                {
                    //the new single modified is major than the old
                    changed = (
                                    source: null,
                                    current: CurrentInfo.Modified,
                                    last: LastState.Modified
                                );
                    return true;
                }
                else
                {
                    changed = default;
                    return false;
                }
            }
            else if (LastState == null)
            {
                //new resource
                changed = (
                                source: null,
                                current: CurrentInfo.Modified,
                                last: null
                            );

                return true;
            }
            else
            {
                changed = default;
                return false;
            }
        }

        throw new InvalidOperationException("Developer bug. One between Modified or ModifiedSources must be populated.");
    }
}

public sealed class ChangedStateContext<T> where T : IResourceState
{
    public ChangedStateContext(IResourceMetadata info, IResourceTrackedState? lastState, AsyncLazy<T?> payload)
    {
        Info = info;
        Payload = payload;
        LastState = lastState;
    }

    public AsyncLazy<T?> Payload { get; }
    public IResourceMetadata Info { get; }
    public IResourceTrackedState? LastState { get; }
}