// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
#pragma warning disable IDE0005 // Using directive is unnecessary - false positive, needed for UnconditionalSuppressMessage

using NLog;

using NodaTime;
using NodaTime.Text;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.ResourceWatcher;

internal sealed class ResourceWatcherDiagnosticSource
{
    public const string DiagnosticListenerName = "Ark.Tools.ResourceWatcher";
    public const string BaseActivityName = "Ark.Tools.ResourceWatcher";
    public const string ExceptionEventName = BaseActivityName + "Exception";

    private readonly string _tenant;
    private readonly Logger _logger;

    private static readonly DiagnosticListener _source = new(DiagnosticListenerName);

    public ResourceWatcherDiagnosticSource(string tenant, Logger logger)
    {
        _tenant = tenant;
        _logger = logger;
    }

    #region Event
#pragma warning disable CA1822 // Mark members as static
    public void HostStartEvent()
#pragma warning restore CA1822 // Mark members as static
    {
        ResourceWatcherDiagnosticSource._reportEvent("HostStartEvent", () => new { });
    }

    public void RunTookTooLong(Activity activity)
    {
        _logger.Fatal($"Check for tenant {_tenant} took too much:{activity.Duration}");

        ResourceWatcherDiagnosticSource._reportEvent("RunTookTooLong",
            () => new
            {
                Activity = activity,
                Tenant = _tenant,
            });
    }

    public void ProcessResourceTookTooLong(string resourceId, Activity activity)
    {
        _logger.Fatal($"Processing of ResourceId={resourceId} took too much: {activity.Duration}");

        ResourceWatcherDiagnosticSource._reportEvent("ProcessResourceTookTooLong",
            () => new
            {
                ResourceId = resourceId,
                Activity = activity,
                Tenant = _tenant,
            });
    }
    #endregion

    #region Run
    public Activity RunStart(RunType type, DateTime now)
    {
        _logger.Info(CultureInfo.InvariantCulture, "Check started for tenant {Tenant} at {Now}", _tenant, now);

        Activity activity = ResourceWatcherDiagnosticSource._start("Run", () => new
        {
            Type = type,
            Now = now,
            Tenant = _tenant,
        }
        );

        return activity;
    }

    public void RunFailed(Activity activity, Exception ex)
    {
        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            Exception = ex,
            Elapsed = activity.Duration,
            Tenant = _tenant,
        }
        );

        _logger.Error(ex, $"Check failed for tenant {_tenant} in {activity.Duration}");
    }

    public void RunSuccessful(Activity activity, Dictionary<ResultType, int> resultCounts)
    {
        ResourceWatcherDiagnosticSource._stop(activity, () =>
        {
            var total = resultCounts.Values.Sum();

            return new
            {
                ResourcesFound = total,
                Normal = resultCounts.GetValueOrDefault(ResultType.Normal, 0),
                NoNewData = resultCounts.GetValueOrDefault(ResultType.NoNewData, 0),
                NoAction = resultCounts.GetValueOrDefault(ResultType.NoAction, 0),
                Error = resultCounts.GetValueOrDefault(ResultType.Error, 0),
                Skipped = resultCounts.GetValueOrDefault(ResultType.Skipped, 0),
                Tenant = _tenant,
            };
        }
        );

        _logger.Info(CultureInfo.InvariantCulture, "Check successful for tenant {Tenant} in {Duration}", _tenant, activity?.Duration);
    }
    #endregion

    #region GetResources
#pragma warning disable CA1822 // Mark members as static
    public Activity GetResourcesStart()
#pragma warning restore CA1822 // Mark members as static
    {
        Activity activity = ResourceWatcherDiagnosticSource._start("GetResources", () => new
        {
        }
        );

        return activity;
    }

    public void GetResourcesFailed(Activity activity, Exception ex)
    {
        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            Exception = ex,
            Tenant = _tenant,
        }
        );
    }

    public void GetResourcesSuccessful(Activity activity, int count)
    {
        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            ResourcesFound = count,
            Elapsed = activity.Duration,
            Tenant = _tenant,
        }
        );

        _logger.Info(CultureInfo.InvariantCulture, "Found {ResourceCount} resources in {Duration}", count, activity?.Duration);
    }
    #endregion

    #region CheckState
#pragma warning disable CA1822 // Mark members as static
    public Activity CheckStateStart()
#pragma warning restore CA1822 // Mark members as static
    {
        Activity activity = ResourceWatcherDiagnosticSource._start("CheckState", () => new
        {
        }
        );

        return activity;
    }

    public void CheckStateSuccessful(Activity activity, Dictionary<ProcessType, int> processCounts)
    {
        ResourceWatcherDiagnosticSource._stop(activity, () =>
        {
            return new
            {
                ResourcesNew = processCounts.GetValueOrDefault(ProcessType.New, 0),
                ResourcesUpdated = processCounts.GetValueOrDefault(ProcessType.Updated, 0),
                ResourcesRetried = processCounts.GetValueOrDefault(ProcessType.Retry, 0),
                ResourcesRetriedAfterBan = processCounts.GetValueOrDefault(ProcessType.RetryAfterBan, 0),
                ResourcesBanned = processCounts.GetValueOrDefault(ProcessType.Banned, 0),
                ResourcesNothingToDo = processCounts.GetValueOrDefault(ProcessType.NothingToDo, 0),
                Tenant = _tenant,
            };
        }
        );
    }


    public void CheckStateFailed(Activity activity, Exception ex)
    {
        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            Exception = ex,
            Tenant = _tenant,
        }
        );
    }
    #endregion

    #region ProcessResource
    public Activity ProcessResourceStart(string resourceId, int? index, int? total, int? lastRetryCount, bool isResourceUpdated, string? modifiedSource, LocalDateTime? currentModified, LocalDateTime? lastModified, ProcessType processType)
    {
        if (!isResourceUpdated)
        {
            _logger.Info(CultureInfo.InvariantCulture, "No changes detected on ResourceId={ResourceId}"
             , resourceId
            );
        }
        else
        {
            _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) Detected change on ResourceId={ResourceId}, Resource.ModifiedSource={ModifiedSource}, Resource.Modified={Modified}, OldState.Modified={OldModified}, OldState.Retry={OldRetryCount}. Processing..."
                , index
                , total
                , resourceId
                , modifiedSource ?? string.Empty
                , currentModified != null ? LocalDateTimePattern.ExtendedIso.Format(currentModified.Value) : "null"
                , lastModified != null ? LocalDateTimePattern.ExtendedIso.Format(lastModified.Value) : "null"
                , lastRetryCount
            );
        }

        Activity activity = ResourceWatcherDiagnosticSource._start("ProcessResource", () => new
        {
            ResourceId = resourceId,
            Index = index,
            Total = total,
            ProcessType = processType,
            ModifiedSource = modifiedSource,
            CurrentModified = currentModified,
            LastModified = lastModified,
            Tenant = _tenant,
        });

        return activity;
    }

    public void ProcessResourceFailed(Activity activity, string resourceId, int? index, int? total, ProcessType processType, bool isBanned, Exception ex)
    {
        var lvl = isBanned ? LogLevel.Fatal : LogLevel.Warn;
        _logger.Log(lvl, ex, CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} process Failed", index, total, resourceId);

        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            ResourceId = resourceId,
            Index = index,
            Total = total,
            ProcessType = processType,
            Exception = ex,
            Tenant = _tenant,
        });
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Anonymous object properties are statically known and safe for trimming")]
    public void ProcessResourceSuccessful(Activity activity, string resourceId, int? index, int? total, ProcessType processType, ResultType? resultType, int? newRetryCount)
    {
        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            ResourceId = resourceId,
            Index = index,
            Total = total,
            ProcessType = processType,
            ResultType = resultType,
            Tenant = _tenant,
        });

        // Emit explicit diagnostic event for listeners that need structured data
        if (_source.IsEnabled("Ark.Tools.ResourceWatcher.ProcessResource.Stop"))
        {
            _source.Write("Ark.Tools.ResourceWatcher.ProcessResource.Stop", new
            {
                Tenant = _tenant,
                ResourceId = resourceId,
                Index = index,
                Total = total,
                ProcessType = processType,
                ResultType = resultType,
                NewRetryCount = newRetryCount,
                Exception = (Exception?)null
            });
        }

        if (resultType == ResultType.NoNewData)
        {
            _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} No payload retrived, so no new state. Generally due to a same-checksum", index, total, resourceId);
        }
        else if (resultType == ResultType.NoAction)
        {
            _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} No action has been triggered and payload has not been retrieved. We do not change the state", index, total, resourceId);
        }
        else if (resultType == ResultType.Normal)
        {
            if (newRetryCount == 0)
                _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} handled successfully in {Duration}", index, total, resourceId, activity?.Duration);
            else
                _logger.Info(CultureInfo.InvariantCulture, "({Index}/{Total}) ResourceId={ResourceId} handled not successfully in {Duration}", index, total, resourceId, activity?.Duration);
        }
    }
    #endregion

    #region FetchResource
    public Activity FetchResourceStart(string resourceId, int? index, int? total, ProcessType processType)
    {
        Activity activity = ResourceWatcherDiagnosticSource._start("FetchResource", () => new
        {
            ResourceId = resourceId,
            Index = index,
            Total = total,
            ProcessType = processType,
            Tenant = _tenant,
        }
        );

        return activity;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Anonymous object properties are statically known and safe for trimming")]
    public void FetchResourceFailed(Activity activity, string resourceId, int? index, int? total, ProcessType processType, Exception ex)
    {
        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            ResourceId = resourceId,
            Index = index,
            Total = total,
            ProcessType = processType,
            Exception = ex,
            Tenant = _tenant,
        }
        );

        // Emit explicit diagnostic event for listeners
        if (_source.IsEnabled("Ark.Tools.ResourceWatcher.FetchResource.Stop"))
        {
            _source.Write("Ark.Tools.ResourceWatcher.FetchResource.Stop", new
            {
                Tenant = _tenant,
                ResourceId = resourceId,
                Index = index,
                Total = total,
                ProcessType = processType,
                Exception = ex
            });
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Anonymous object properties are statically known and safe for trimming")]
    public void FetchResourceSuccessful(Activity activity, string resourceId, int? index, int? total, ProcessType processType)
    {
        //_setTags(activity, processType.ToString(), processType.ToString());

        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            ResourceId = resourceId,
            Index = index,
            Total = total,
            ProcessType = processType,
            Tenant = _tenant,
        }
        );

        // Emit explicit diagnostic event for listeners
        if (_source.IsEnabled("Ark.Tools.ResourceWatcher.FetchResource.Stop"))
        {
            _source.Write("Ark.Tools.ResourceWatcher.FetchResource.Stop", new
            {
                Tenant = _tenant,
                ResourceId = resourceId,
                Index = index,
                Total = total,
                ProcessType = processType,
                Exception = (Exception?)null
            });
        }
    }
    #endregion


    #region Exception
    public void ProcessResourceSaveFailed(string resourceId, Exception ex)
    {
        _logger.Error(ex, $"Saving of ResourceId={resourceId} failed");

        _reportException("ProcessResourceSaveFailed", ex, _tenant);
    }

    public void ThrowDuplicateResourceIdRetrived(string duplicateId)
    {
        var ex = new InvalidOperationException($"Found multiple entries for ResouceId: {duplicateId}");

        _reportException("ThrowDuplicateResourceIdRetrived", ex, _tenant);

        throw ex;
    }

    public void ReportRunConsecutiveFailureLimitReached(Exception ex, int count)
    {
        _logger.Fatal($"Failed {count} times consecutively");

        _reportException("ReportRunConsecutiveFailureLimitReached", ex, _tenant);
    }
    #endregion

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Generic type parameter has DynamicallyAccessedMembers annotation. Anonymous types with primitive properties and types marked with DynamicDependency are preserved.")]
    private static Activity _start<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string operationName, Func<T> getPayload, bool unlinkFromParent = false)
    {
        string activityName = BaseActivityName + "." + operationName;

        var activity = new Activity(activityName);

        if (_source.IsEnabled(activityName + ".Start"))
        {
            _source.StartActivity(activity, getPayload());
        }
        else
        {
            activity.Start();
        }

        return activity;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Generic type parameter has DynamicallyAccessedMembers annotation. Anonymous types with primitive properties and types marked with DynamicDependency are preserved.")]
    private static void _stop<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(Activity activity, Func<T> getPayload)
    {
        if (activity != null)
        {
            _source.StopActivity(activity, getPayload());
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Generic type parameter has DynamicallyAccessedMembers annotation. Anonymous types with primitive properties and types marked with DynamicDependency are preserved.")]
    private static void _reportEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string eventName, Func<T> getPayload)
    {
        var name = BaseActivityName + "." + eventName;

        if (_source.IsEnabled(name))
        {
            _source.Write(name, getPayload());
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Anonymous type contains only primitive properties that are always preserved.")]
    private static void _reportException(string exceptionName, Exception ex, string tenant)
    {
        var name = BaseActivityName + "." + exceptionName;

        if (_source.IsEnabled(name))
        {
            _source.Write(name,
                new
                {
                    Exception = ex,
                    Tenant = tenant
                });
        }
    }
}