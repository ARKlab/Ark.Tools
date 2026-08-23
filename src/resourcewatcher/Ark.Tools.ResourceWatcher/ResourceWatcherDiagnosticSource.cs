// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;

using NodaTime;
using NodaTime.Text;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq.Expressions;

namespace Ark.Tools.ResourceWatcher;

internal sealed class ResourceWatcherDiagnosticSource
{
    public const string DiagnosticListenerName = ResourceWatcherInstrumentation.DiagnosticListenerName;
    public const string ActivitySourceName = ResourceWatcherInstrumentation.ActivitySourceName;
    public const string BaseActivityName = ResourceWatcherInstrumentation.ActivityNamePrefix;
    public const string ExceptionEventName = ResourceWatcherInstrumentation.ExceptionEventName;

    private const string _legacyBaseActivityName = "Ark.Tools.ResourceWatcher";
    private readonly string _tenant;
    private readonly Logger _logger;

    private static readonly DiagnosticListener _source = new(DiagnosticListenerName);
    private static readonly ActivitySource _activitySource = new(ActivitySourceName);
    private static readonly Meter _meter = new(ResourceWatcherInstrumentation.MeterName);
    private static readonly Counter<long> _runs = _meter.CreateCounter<long>("ark.tools.resourcewatcher.runs");
    private static readonly Counter<long> _listedResources = _meter.CreateCounter<long>("ark.tools.resourcewatcher.resources.listed");
    private static readonly Counter<long> _processedResources = _meter.CreateCounter<long>("ark.tools.resourcewatcher.resources.processed");

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
        _logger.Fatal(CultureInfo.InvariantCulture, "Check for tenant {Tenant} took too much:{Duration}", _tenant, activity.Duration);

        ResourceWatcherDiagnosticSource._reportEvent("RunTookTooLong",
            () => new
            {
                Activity = activity,
                Tenant = _tenant,
            });
    }

    public void ProcessResourceTookTooLong(string resourceId, Activity activity)
    {
        _logger.Fatal(CultureInfo.InvariantCulture, "Processing of ResourceId={ResourceId} took too much: {Duration}", resourceId, activity.Duration);

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
        _runs.Add(1, new KeyValuePair<string, object?>("outcome", "failed"));
        ResourceWatcherDiagnosticSource._stop(activity, () => new
        {
            Exception = ex,
            Elapsed = activity.Duration,
            Tenant = _tenant,
        }
        );

        _logger.Error(ex, CultureInfo.InvariantCulture, "Check failed for tenant {Tenant} in {Duration}", _tenant, activity.Duration);
    }

    public void RunSuccessful(Activity activity, Dictionary<ResultType, int> resultCounts, int bannedCount)
    {
        var normal = resultCounts.GetValueOrDefault(ResultType.Normal, 0);
        var noNewData = resultCounts.GetValueOrDefault(ResultType.NoNewData, 0);
        var noAction = resultCounts.GetValueOrDefault(ResultType.NoAction, 0);
        var skipped = Math.Max(0, resultCounts.GetValueOrDefault(ResultType.Skipped, 0) - bannedCount);
        var failed = resultCounts.GetValueOrDefault(ResultType.Error, 0);

        _runs.Add(1, new KeyValuePair<string, object?>("outcome", "success"));
        _processedResources.Add(normal, new KeyValuePair<string, object?>("outcome", "success"));
        _processedResources.Add(noNewData, new KeyValuePair<string, object?>("outcome", "no_new_data"));
        _processedResources.Add(noAction, new KeyValuePair<string, object?>("outcome", "no_action"));
        _processedResources.Add(skipped, new KeyValuePair<string, object?>("outcome", "skip"));
        _processedResources.Add(bannedCount, new KeyValuePair<string, object?>("outcome", "banned"));
        _processedResources.Add(failed, new KeyValuePair<string, object?>("outcome", "failed"));

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
        _listedResources.Add(count);
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
        if (_source.IsEnabled())
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
        if (_source.IsEnabled())
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
        if (_source.IsEnabled())
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
        _logger.Error(ex, CultureInfo.InvariantCulture, "Saving of ResourceId={ResourceId} failed", resourceId);

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
        _logger.Fatal(CultureInfo.InvariantCulture, "Failed {Count} times consecutively", count);

        _reportException("ReportRunConsecutiveFailureLimitReached", ex, _tenant);
    }
    #endregion

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Generic type parameter has DynamicallyAccessedMembers annotation. Anonymous types with primitive properties and types marked with DynamicDependency are preserved.")]
    private static Activity _start<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string operationName, Func<T> getPayload, bool unlinkFromParent = false)
    {
        string activityName = BaseActivityName + "." + _toSnakeCase(operationName);
        string legacyActivityName = _legacyBaseActivityName + "." + operationName;
        var payload = getPayload();

        Activity? activity;
        if (_source.IsEnabled())
        {
            activity = new Activity(legacyActivityName);
            _source.StartActivity(activity, payload);
        }
        else
        {
#pragma warning disable CA2000 // The caller owns and disposes the returned activity.
            activity = _activitySource.StartActivity(activityName, ActivityKind.Internal)
                ?? new Activity(activityName).Start();
#pragma warning restore CA2000
        }

        PayloadMapper<T>._setTags(activity, payload);
        return activity;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Generic type parameter has DynamicallyAccessedMembers annotation. Anonymous types with primitive properties and types marked with DynamicDependency are preserved.")]
    private static void _stop<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(Activity activity, Func<T> getPayload)
    {
        if (activity != null)
        {
            var payload = getPayload();
            PayloadMapper<T>._setTags(activity, payload);
            _source.StopActivity(activity, payload);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Generic type parameter has DynamicallyAccessedMembers annotation. Anonymous types with primitive properties and types marked with DynamicDependency are preserved.")]
    private static void _reportEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string eventName, Func<T> getPayload)
    {
        var name = BaseActivityName + "." + _toSnakeCase(eventName);
        var legacyName = _legacyBaseActivityName + "." + eventName;
        var payload = getPayload();

        if (_source.IsEnabled())
        {
            _source.Write(legacyName, payload);
        }

        _addActivityEvent(name, payload);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Anonymous type contains only primitive properties that are always preserved.")]
    private static void _reportException(string exceptionName, Exception ex, string tenant)
    {
        var name = BaseActivityName + "." + _toSnakeCase(exceptionName);
        var legacyName = _legacyBaseActivityName + "." + exceptionName;

        if (_source.IsEnabled())
        {
            _source.Write(legacyName,
                new
                {
                    Exception = ex,
                    Tenant = tenant
                });
        }

        _recordException(Activity.Current, ex);
        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }

    private static void _setMappedTag(Activity activity, string propertyName, object? value)
    {
        if (value is null)
            return;

        activity.SetTag(_toSnakeCase(propertyName), _toTagValue(value));
    }

    private static void _setMappedException(Activity activity, Exception? exception)
    {
        if (exception is null)
            return;

        _recordException(activity, exception);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    private static void _addMappedTag(ActivityTagsCollection tags, string propertyName, object? value)
    {
        if (value is not null)
            tags[_toSnakeCase(propertyName)] = _toTagValue(value);
    }

    private static void _addActivityEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string name, T payload)
    {
        var activity = Activity.Current;
        if (activity is null || payload is null)
            return;

        var tags = new ActivityTagsCollection();
        PayloadMapper<T>._setEventTags(tags, payload);
        activity.AddEvent(new ActivityEvent(name, tags: tags));
    }

    private static object _toTagValue(object value)
    {
        return value switch
        {
            TimeSpan duration => duration.TotalMilliseconds,
            Enum enumValue => enumValue.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static void _recordException(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.AddException(exception);
    }

    private static string _toSnakeCase(string value)
    {
        var chars = new List<char>(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsUpper(character) && i > 0)
                chars.Add('_');

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string(chars.ToArray());
    }

    private static class PayloadMapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>
    {
        internal static readonly Action<Activity, T> _setTags = _createTagMapper();
        internal static readonly Action<ActivityTagsCollection, T> _setEventTags = _createEventTagMapper();

        private static Action<Activity, T> _createTagMapper()
        {
            var activity = Expression.Parameter(typeof(Activity), "activity");
            var payload = Expression.Parameter(typeof(T), "payload");
            var statements = new List<Expression>();

            foreach (var property in typeof(T).GetProperties())
            {
                var value = Expression.Property(payload, property);
                if (typeof(Activity).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                if (typeof(Exception).IsAssignableFrom(property.PropertyType))
                {
                    statements.Add(Expression.Call(
                        typeof(ResourceWatcherDiagnosticSource),
                        nameof(_setMappedException),
                        null,
                        activity,
                        value));
                }
                else
                {
                    statements.Add(Expression.Call(
                        typeof(ResourceWatcherDiagnosticSource),
                        nameof(_setMappedTag),
                        null,
                        activity,
                        Expression.Constant(property.Name),
                        Expression.Convert(value, typeof(object))));
                }
            }

            return Expression.Lambda<Action<Activity, T>>(Expression.Block(statements), activity, payload).Compile();
        }

        private static Action<ActivityTagsCollection, T> _createEventTagMapper()
        {
            var tags = Expression.Parameter(typeof(ActivityTagsCollection), "tags");
            var payload = Expression.Parameter(typeof(T), "payload");
            var statements = new List<Expression>();

            foreach (var property in typeof(T).GetProperties())
            {
                if (typeof(Activity).IsAssignableFrom(property.PropertyType)
                    || typeof(Exception).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                statements.Add(Expression.Call(
                    typeof(ResourceWatcherDiagnosticSource),
                    nameof(_addMappedTag),
                    null,
                    tags,
                    Expression.Constant(property.Name),
                    Expression.Convert(Expression.Property(payload, property), typeof(object))));
            }

            return Expression.Lambda<Action<ActivityTagsCollection, T>>(Expression.Block(statements), tags, payload).Compile();
        }
    }
}
