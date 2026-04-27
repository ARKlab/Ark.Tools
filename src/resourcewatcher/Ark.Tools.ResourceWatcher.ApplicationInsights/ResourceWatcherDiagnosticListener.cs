using Ark.Tools.NewtonsoftJson;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DiagnosticAdapter;

using Newtonsoft.Json;

using NodaTime.Text;

using System.Diagnostics;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights;

public class ResourceWatcherDiagnosticListener : ResourceWatcherDiagnosticListenerBase
{
    private readonly TelemetryClient _client;

    private const string _type = "ProcessStep";

    public ResourceWatcherDiagnosticListener(TelemetryConfiguration configuration)
    {
        this._client = new TelemetryClient(configuration);
    }

    #region Event
    [DiagnosticName("Ark.Tools.ResourceWatcher.HostStartEvent")]
    public override void OnHostStartEvent()
    {
        var telemetry = new EventTelemetry
        {
            Name = "Ark.Tools.ResourceWatcher.HostStartEvent",
        };

        this._client.TrackEvent(telemetry);
    }

    [DiagnosticName("Ark.Tools.ResourceWatcher.RunTookTooLong")]
    public override void RunTookTooLong(string tenant, Activity activity)
    {
        var telemetry = new EventTelemetry
        {
            Name = activity.OperationName,
        };

        telemetry.Timestamp = new DateTimeOffset(activity.StartTimeUtc, TimeSpan.Zero);

        //Properties and metrics
        telemetry.Properties.Add("Tenant", tenant);
        telemetry.Properties.Add("ElapsedSeconds", activity.Duration.TotalSeconds.ToString("G", CultureInfo.InvariantCulture));
        telemetry.Properties.Add("ElapsedMinutes", activity.Duration.Minutes.ToString(CultureInfo.InvariantCulture));

        this._client.TrackEvent(telemetry);
    }

    [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResourceTookTooLong")]
    public override void OnProcessResourceTookTooLong(string tenant, string resourceId, Activity activity)
    {
        var telemetry = new EventTelemetry
        {
            Name = activity.OperationName,
        };

        telemetry.Timestamp = new DateTimeOffset(activity.StartTimeUtc, TimeSpan.Zero);

        //Properties and metrics
        telemetry.Properties.Add("Tenant", tenant);
        telemetry.Properties.Add("ResourceId", resourceId);
        telemetry.Properties.Add("ElapsedSeconds", activity.Duration.TotalSeconds.ToString("G", CultureInfo.InvariantCulture));
        telemetry.Properties.Add("ElapsedMinutes", activity.Duration.Minutes.ToString(CultureInfo.InvariantCulture));

        this._client.TrackEvent(telemetry);
    }
    #endregion

    #region Exception
    [DiagnosticName("Ark.Tools.ResourceWatcher.ThrowDuplicateResourceIdRetrived")]
    public override void OnDuplicateResourceIdRetrived(string tenant, Exception exception)
    {
        var telemetryException = new ExceptionTelemetry
        {
            Exception = exception,
            Message = exception.Message
        };

        telemetryException.Properties.Add("Tenant", tenant);

        this._client.TrackException(telemetryException);
    }

    [DiagnosticName("Ark.Tools.ResourceWatcher.ReportRunConsecutiveFailureLimitReached")]
    public override void OnReportRunConsecutiveFailureLimitReached(string tenant, Exception exception)
    {
        var telemetryException = new ExceptionTelemetry
        {
            Exception = exception,
            Message = exception.Message
        };

        telemetryException.Properties.Add("Tenant", tenant);

        this._client.TrackException(telemetryException);
    }

    [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResourceSaveFailed")]
    public override void OnProcessResourceSaveFailed(string resourceId, string tenant, Exception exception)
    {
        var telemetryException = new ExceptionTelemetry
        {
            Exception = exception,
            Message = exception.Message
        };

        telemetryException.Properties.Add("Tenant", tenant);

        this._client.TrackException(telemetryException);
    }
    #endregion

    #region Run
    [DiagnosticName("Ark.Tools.ResourceWatcher.Run.Stop")]
    public override void OnRunStop(int resourcesFound
                                    , int normal
                                    , int noPayload
                                    , int noAction
                                    , int error
                                    , int skipped
                                    , string tenant
                                    , Exception exception)
    {
        var currentActivity = Activity.Current;
        if (currentActivity == null)
            return;

        var telemetry = new RequestTelemetry
        {
            Id = currentActivity.Id,
            Duration = currentActivity.Duration,
            Name = currentActivity.OperationName,
            Success = exception == null ? true : false,
            Timestamp = new DateTimeOffset(currentActivity.StartTimeUtc, TimeSpan.Zero),
        };

        //Properties and metrics
        telemetry.Properties.Add("Tenant", tenant);
        telemetry.Properties.Add("ResourcesFound", resourcesFound.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Result_Normal", normal.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Result_NoNewData", noPayload.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Result_NoAction", noAction.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Result_Error", error.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Result_Skipped", skipped.ToString(CultureInfo.InvariantCulture));

        //Exception
        if (exception != null)
        {
            var telemetryException = new ExceptionTelemetry
            {
                Exception = exception,
                Message = exception.Message
            };

            //Properties and metrics
            telemetryException.Properties.Add("Tenant", tenant);
            telemetryException.Properties.Add("ResourcesFound", resourcesFound.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Result_Normal", normal.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Result_NoNewData", noPayload.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Result_NoAction", noAction.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Result_Error", error.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Result_Skipped", skipped.ToString(CultureInfo.InvariantCulture));

            this._client.TrackException(telemetryException);
        }

        this._client.TrackRequest(telemetry);
    }
    #endregion 

    #region GetResources
    [DiagnosticName("Ark.Tools.ResourceWatcher.GetResources.Stop")]
    public override void OnGetResourcesStop(int resourcesFound, TimeSpan elapsed, string tenant, Exception exception)
    {
        var currentActivity = Activity.Current;
        if (currentActivity == null) return;

        var telemetry = new DependencyTelemetry
        {
            Id = currentActivity.Id,
            Duration = currentActivity.Duration,
            Name = currentActivity.OperationName,
            Success = exception == null ? true : false,
            Timestamp = new DateTimeOffset(currentActivity.StartTimeUtc, TimeSpan.Zero),
            Type = _type
        };

        //Properties and metrics
        telemetry.Properties.Add("Tenant", tenant);
        telemetry.Properties.Add("Elapsed", elapsed.ToString());
        telemetry.Properties.Add("ResourcesFound", resourcesFound.ToString(CultureInfo.InvariantCulture));

        //Exception
        if (exception != null)
        {
            var telemetryException = new ExceptionTelemetry
            {
                Exception = exception,
                Message = exception.Message
            };

            telemetryException.Properties.Add("Tenant", tenant);
            telemetryException.Properties.Add("ResourcesFound", resourcesFound.ToString(CultureInfo.InvariantCulture));

            this._client.TrackException(telemetryException);
        }

        this._client.TrackDependency(telemetry);
    }
    #endregion

    #region CheckState

    [DiagnosticName("Ark.Tools.ResourceWatcher.CheckState.Stop")]
    public override void OnCheckStateStop(int resourcesNew
                                            , int resourcesUpdated
                                            , int resourcesRetried
                                            , int resourcesRetriedAfterBan
                                            , int resourcesBanned
                                            , int resourcesNothingToDo
                                            , string tenant
                                            , Exception exception)
    {
        var currentActivity = Activity.Current;
        if (currentActivity == null) return;

        var telemetry = new DependencyTelemetry
        {
            Id = currentActivity.Id,
            Duration = currentActivity.Duration,
            Name = currentActivity.OperationName,
            Success = exception == null ? true : false,
            Timestamp = new DateTimeOffset(currentActivity.StartTimeUtc, TimeSpan.Zero),
            Type = _type
        };

        //Properties and metrics
        telemetry.Properties.Add("Tenant", tenant);
        telemetry.Properties.Add("Resources_New", resourcesNew.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Resources_Updated", resourcesUpdated.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Resources_Retried", resourcesRetried.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Resources_RetriedAfterBan", resourcesRetriedAfterBan.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Resources_Banned", resourcesBanned.ToString(CultureInfo.InvariantCulture));
        telemetry.Properties.Add("Resources_NothingToDo", resourcesNothingToDo.ToString(CultureInfo.InvariantCulture));

        //Exception
        if (exception != null)
        {
            var telemetryException = new ExceptionTelemetry
            {
                Exception = exception,
                Message = exception.Message
            };

            telemetryException.Properties.Add("Tenant", tenant);

            //Properties and metrics
            telemetryException.Properties.Add("Resources_New", resourcesNew.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Resources_Updated", resourcesUpdated.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Resources_Retried", resourcesRetried.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Resources_RetriedAfterBan", resourcesRetriedAfterBan.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Resources_Banned", resourcesBanned.ToString(CultureInfo.InvariantCulture));
            telemetryException.Properties.Add("Resources_NothingToDo", resourcesNothingToDo.ToString(CultureInfo.InvariantCulture));

            this._client.TrackException(telemetryException);
        }

        this._client.TrackDependency(telemetry);
    }
    #endregion

    #region ProcessEntry
    [DiagnosticName("Ark.Tools.ResourceWatcher.ProcessResource.Stop")]
    public override void OnProcessResourceStop(string tenant, string resourceId, int? index, int? total, ProcessType processType, ResultType? resultType, int? newRetryCount, Exception exception)
    {
        var currentActivity = Activity.Current;
        if (currentActivity == null) return;

        var telemetry = new RequestTelemetry
        {
            Id = currentActivity.Id,
            Duration = currentActivity.Duration,
            Name = currentActivity.OperationName,
            Success = exception == null ? true : false,
            Timestamp = new DateTimeOffset(currentActivity.StartTimeUtc, TimeSpan.Zero),
        };

        //Properties and metrics
        telemetry.Properties.Add("Tenant", tenant);
        telemetry.Properties.Add("ResourceId", resourceId);
        telemetry.Properties.Add("ProcessType", processType.ToString());
        if (resultType.HasValue)
            telemetry.Properties.Add("ResultType", resultType.Value.ToString());
        telemetry.Properties.Add("Idx/Total", index?.ToString(CultureInfo.InvariantCulture) + "/" + total?.ToString(CultureInfo.InvariantCulture));

        //Exception
        if (exception != null)
        {
            var telemetryException = new ExceptionTelemetry
            {
                Exception = exception,
                Message = exception.Message
            };

            //Properties and metrics
            telemetryException.Properties.Add("Tenant", tenant);
            telemetryException.Properties.Add("ResourceId", resourceId);
            telemetryException.Properties.Add("ProcessType", processType.ToString());
            if (resultType.HasValue)
                telemetryException.Properties.Add("ResultType", resultType.Value.ToString());
            telemetryException.Properties.Add("Idx/Total", index?.ToString(CultureInfo.InvariantCulture) + "/" + total?.ToString(CultureInfo.InvariantCulture));

            this._client.TrackException(telemetryException);
        }

        this._client.TrackRequest(telemetry);
    }

    #endregion

    #region FetchResource
    [DiagnosticName("Ark.Tools.ResourceWatcher.FetchResource.Stop")]
    public override void OnFetchResourceStop(string tenant, string resourceId, int? index, int? total, ProcessType processType, Exception exception)
    {
        var currentActivity = Activity.Current;
        if (currentActivity == null) return;

        var telemetry = new DependencyTelemetry
        {
            Id = currentActivity.Id,
            Duration = currentActivity.Duration,
            Name = currentActivity.OperationName,
            Success = exception == null ? true : false,
            Timestamp = new DateTimeOffset(currentActivity.StartTimeUtc, TimeSpan.Zero),
            Type = _type
        };

        //Properties and metrics
        telemetry.Properties.Add("Tenant", tenant);
        telemetry.Properties.Add("ResourceId", resourceId);
        telemetry.Properties.Add("ProcessType", processType.ToString());
        telemetry.Properties.Add("Idx/Total", index?.ToString(CultureInfo.InvariantCulture) + "/" + total?.ToString(CultureInfo.InvariantCulture));

        //Exception
        if (exception != null)
        {
            var telemetryException = new ExceptionTelemetry
            {
                Exception = exception,
                Message = exception.Message
            };

            //Properties and metrics
            telemetryException.Properties.Add("Tenant", tenant);
            telemetryException.Properties.Add("ResourceId", resourceId);
            telemetryException.Properties.Add("ProcessType", processType.ToString());
            telemetryException.Properties.Add("Idx/Total", index?.ToString(CultureInfo.InvariantCulture) + "/" + total?.ToString(CultureInfo.InvariantCulture));

            this._client.TrackException(telemetryException);
        }

        this._client.TrackDependency(telemetry);
    }
    #endregion



    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Serializes Extensions dictionary for Application Insights telemetry. Extensions is a well-known Dictionary<string, string> with primitive values that are always preserved. Telemetry data is optional and does not affect core functionality.")]
    private static void _propertiesProcessContext(ISupportProperties data, ProcessContext pc)
    {
        data.Properties.Add("ResourceId", pc.CurrentInfo.ResourceId);
        data.Properties.Add("ProcessType", pc.ProcessType.ToString());
        data.Properties.Add("ResultType", pc.ResultType.ToString());

        data.Properties.Add("Idx/Total", pc.Index?.ToString(CultureInfo.InvariantCulture) + "/" + pc.Total?.ToString(CultureInfo.InvariantCulture));

        if (pc.LastState != default)
        {
            data.Properties.Add("CheckSum_Old", pc.LastState.CheckSum);
            data.Properties.Add("Modified_Old", LocalDateTimePattern.ExtendedIso.Format(pc.LastState.Modified));
            if (pc.LastState.ModifiedSources != null && pc.LastState.ModifiedSources.Count != 0)
            {
                foreach (var modified in pc.LastState.ModifiedSources)
                {
                    data.Properties.Add("Modified" + modified.Key + "_Old", LocalDateTimePattern.ExtendedIso.Format(modified.Value));
                }
            }
        }
        if (pc.NewState != default)
        {
            data.Properties.Add("RetryCount", pc.NewState.RetryCount.ToString(CultureInfo.InvariantCulture));
            data.Properties.Add("RetrievedAt", pc.NewState.ToString());
            data.Properties.Add("CheckSum", pc.NewState.CheckSum);
            data.Properties.Add("Modified", LocalDateTimePattern.ExtendedIso.Format(pc.NewState.Modified));

            if (pc.NewState.ModifiedSources != null && pc.NewState.ModifiedSources.Count != 0)
            {
                foreach (var modified in pc.NewState.ModifiedSources)
                {
                    data.Properties.Add("Modified" + modified.Key, LocalDateTimePattern.ExtendedIso.Format(modified.Value));
                }
            }

            string extensionsString = JsonConvert.SerializeObject(pc.NewState.Extensions, ArkDefaultJsonSerializerSettings.Instance);
            data.Properties.Add("Extensions", extensionsString);
        }
    }
}