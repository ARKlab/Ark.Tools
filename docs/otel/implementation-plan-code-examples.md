# OpenTelemetry Custom Sampler - Code Examples

## Complete Implementation Reference

This document provides complete, runnable code examples for implementing the custom OpenTelemetry sampler for Ark.Tools.

---

## 1. ArkAdaptiveSampler.cs

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights.OpenTelemetry;

/// <summary>
/// Adaptive sampler that preserves failures and applies per-operation rate limiting.
/// </summary>
public sealed class ArkAdaptiveSampler : Sampler
{
    private readonly ArkAdaptiveSamplerOptions _options;
    private readonly ConcurrentDictionary<string, OperationBucket> _buckets;
    private readonly AdaptiveRateController _rateController;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ArkAdaptiveSampler"/> class.
    /// </summary>
    /// <param name="options">Sampler configuration options.</param>
    public ArkAdaptiveSampler(ArkAdaptiveSamplerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _buckets = new ConcurrentDictionary<string, OperationBucket>(StringComparer.OrdinalIgnoreCase);
        _rateController = new AdaptiveRateController(options);
    }
    
    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var activity = samplingParameters.ParentContext.ActivityContext.SpanId != default
            ? Activity.Current
            : null;
        
        // Always sample if this is a failure/error
        if (IsFailureOrError(activity, samplingParameters))
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }
        
        // Get operation name for bucketing
        var operationName = GetOperationName(activity, samplingParameters);
        
        // Apply per-operation rate limiting if enabled
        if (_options.EnablePerOperationBucketing)
        {
            var bucket = GetOrCreateBucket(operationName);
            if (bucket.TryConsume())
            {
                return new SamplingResult(SamplingDecision.RecordAndSample);
            }
            return new SamplingResult(SamplingDecision.Drop);
        }
        
        // Global rate limiting (fallback)
        var globalBucket = GetOrCreateBucket("__global__");
        if (globalBucket.TryConsume())
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }
        
        return new SamplingResult(SamplingDecision.Drop);
    }
    
    /// <inheritdoc/>
    public override string Description => $"ArkAdaptiveSampler{{rate={_options.TracesPerSecond}/s}}";
    
    private static bool IsFailureOrError(Activity? activity, in SamplingParameters parameters)
    {
        // Check Activity status
        if (activity?.Status == ActivityStatusCode.Error)
            return true;
        
        // Check for exception events
        if (activity?.Events.Any(e => e.Name == "exception") == true)
            return true;
        
        // Check HTTP status code in tags
        if (parameters.Tags != null)
        {
            foreach (var tag in parameters.Tags)
            {
                if (tag.Key == "http.response.status_code" && tag.Value is int statusCode)
                {
                    if (statusCode >= 400)
                        return true;
                }
                if (tag.Key == "otel.status_code" && "ERROR".Equals(tag.Value as string, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private static string GetOperationName(Activity? activity, in SamplingParameters parameters)
    {
        // Try to get operation name from Activity
        if (!string.IsNullOrEmpty(activity?.DisplayName))
            return activity.DisplayName;
        
        // Try to get from parameters
        if (!string.IsNullOrEmpty(parameters.Name))
            return parameters.Name;
        
        // Fallback
        return "unknown";
    }
    
    private OperationBucket GetOrCreateBucket(string operationName)
    {
        return _buckets.GetOrAdd(operationName, name =>
        {
            // Enforce max buckets limit
            if (_buckets.Count >= _options.MaxOperationBuckets)
            {
                // Use global bucket when limit reached
                return _buckets.GetOrAdd("__global__", _ => 
                    new OperationBucket(_rateController.GetCurrentRate(), _options));
            }
            
            return new OperationBucket(_rateController.GetCurrentRate(), _options);
        });
    }
}
```

---

## 2. OperationBucket.cs

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.ApplicationInsights.OpenTelemetry;

/// <summary>
/// Token bucket for per-operation rate limiting.
/// </summary>
internal sealed class OperationBucket
{
    private readonly object _lock = new object();
    private readonly double _capacity;
    private double _tokens;
    private DateTime _lastRefill;
    private readonly ArkAdaptiveSamplerOptions _options;
    
    public OperationBucket(double rate, ArkAdaptiveSamplerOptions options)
    {
        _options = options;
        _capacity = rate * 2.0; // Allow burst up to 2 seconds worth
        _tokens = _capacity;
        _lastRefill = DateTime.UtcNow;
    }
    
    public bool TryConsume()
    {
        lock (_lock)
        {
            Refill();
            
            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                return true;
            }
            
            return false;
        }
    }
    
    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        
        // Calculate tokens to add based on configured rate
        var tokensToAdd = elapsed * _options.TracesPerSecond;
        
        // Refill up to capacity
        _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
        _lastRefill = now;
    }
    
    public double CurrentSamplingPercentage
    {
        get
        {
            lock (_lock)
            {
                // Rough estimate of current sampling percentage
                return (_tokens / _capacity) * 100.0;
            }
        }
    }
}
```

---

## 3. AdaptiveRateController.cs

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.ApplicationInsights.OpenTelemetry;

/// <summary>
/// Controls adaptive rate adjustment based on observed telemetry volume.
/// </summary>
internal sealed class AdaptiveRateController
{
    private readonly ArkAdaptiveSamplerOptions _options;
    private readonly object _lock = new object();
    private double _currentRate;
    private DateTime _lastAdjustment;
    private int _sampledCount;
    private int _droppedCount;
    
    public AdaptiveRateController(ArkAdaptiveSamplerOptions options)
    {
        _options = options;
        _currentRate = options.TracesPerSecond;
        _lastAdjustment = DateTime.UtcNow;
    }
    
    public double GetCurrentRate()
    {
        lock (_lock)
        {
            AdjustRateIfNeeded();
            return _currentRate;
        }
    }
    
    public void RecordSample(bool sampled)
    {
        lock (_lock)
        {
            if (sampled)
                _sampledCount++;
            else
                _droppedCount++;
        }
    }
    
    private void AdjustRateIfNeeded()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastAdjustment;
        
        // Only adjust if enough time has passed
        if (elapsed < _options.SamplingPercentageDecreaseTimeout)
            return;
        
        if (_sampledCount == 0 && _droppedCount == 0)
            return;
        
        // Calculate current telemetry rate
        var totalItems = _sampledCount + _droppedCount;
        var observedRatePerSecond = totalItems / elapsed.TotalSeconds;
        
        // Calculate current sampling percentage
        var currentSamplingPercentage = _sampledCount / (double)totalItems;
        
        // Calculate what percentage we SHOULD be at for target rate
        var targetSamplingPercentage = _options.TracesPerSecond / observedRatePerSecond;
        targetSamplingPercentage = Math.Clamp(targetSamplingPercentage, 0.001, 1.0);
        
        // Apply moving average for smooth transitions
        var newSamplingPercentage = (_options.MovingAverageRatio * currentSamplingPercentage) +
                                    ((1.0 - _options.MovingAverageRatio) * targetSamplingPercentage);
        
        // Update rate based on new sampling percentage
        _currentRate = observedRatePerSecond * newSamplingPercentage;
        _currentRate = Math.Max(0.001, _currentRate); // Minimum rate
        
        // Reset counters
        _sampledCount = 0;
        _droppedCount = 0;
        _lastAdjustment = now;
    }
}
```

---

## 4. ArkPreFilterProcessor.cs

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;
using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights.OpenTelemetry;

/// <summary>
/// Filters out high-volume, low-value telemetry before sampling.
/// Equivalent to ArkSkipUselessSpamTelemetryProcessor from v2.x.
/// </summary>
public sealed class ArkPreFilterProcessor : BaseProcessor<Activity>
{
    /// <summary>
    /// Called when an Activity starts.
    /// </summary>
    public override void OnStart(Activity data)
    {
        if (ShouldFilter(data))
        {
            // Mark as not recorded to prevent export
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            data.IsAllDataRequested = false;
        }
    }
    
    private static bool ShouldFilter(Activity activity)
    {
        // Filter successful OPTIONS requests (CORS preflight)
        var httpMethod = activity.GetTagItem("http.request.method") as string;
        if ("OPTIONS".Equals(httpMethod, StringComparison.OrdinalIgnoreCase))
        {
            // Only filter if successful (errors should still be tracked)
            var statusCode = activity.GetTagItem("http.response.status_code");
            if (statusCode is int code && code < 400)
                return true;
        }
        
        // Filter Azure Service Bus Receive operations (successful only)
        var messagingOperation = activity.GetTagItem("messaging.operation") as string;
        var messagingSystem = activity.GetTagItem("messaging.system") as string;
        
        if ("receive".Equals(messagingOperation, StringComparison.OrdinalIgnoreCase) &&
            "servicebus".Equals(messagingSystem, StringComparison.OrdinalIgnoreCase))
        {
            // Only filter successful receives
            if (activity.Status != ActivityStatusCode.Error)
                return true;
        }
        
        // Filter ServiceBusReceiver.* operations
        if (activity.DisplayName?.StartsWith("ServiceBusReceiver.", StringComparison.OrdinalIgnoreCase) == true &&
            activity.Status != ActivityStatusCode.Error)
        {
            return true;
        }
        
        // Filter SQL Commit operations (successful only)
        var dbOperation = activity.GetTagItem("db.operation") as string;
        var dbSystem = activity.GetTagItem("db.system") as string;
        
        if ("Commit".Equals(dbOperation, StringComparison.OrdinalIgnoreCase) &&
            "mssql".Equals(dbSystem, StringComparison.OrdinalIgnoreCase))
        {
            if (activity.Status != ActivityStatusCode.Error)
                return true;
        }
        
        return false;
    }
}
```

---

## 5. ArkTelemetryEnrichmentProcessor.cs

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;
using System.Diagnostics;
using System.Reflection;

namespace Ark.Tools.ApplicationInsights.OpenTelemetry;

/// <summary>
/// Enriches activities with global information.
/// Equivalent to GlobalInfoTelemetryInitializer from v2.x.
/// </summary>
public sealed class ArkTelemetryEnrichmentProcessor : BaseProcessor<Activity>
{
    private readonly string? _processName;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ArkTelemetryEnrichmentProcessor"/> class.
    /// </summary>
    public ArkTelemetryEnrichmentProcessor()
    {
        _processName = Assembly.GetEntryAssembly()?.GetName().Name;
    }
    
    /// <summary>
    /// Called when an Activity starts.
    /// </summary>
    public override void OnStart(Activity data)
    {
        if (_processName != null && data.GetTagItem("ProcessName") == null)
        {
            data.SetTag("ProcessName", _processName);
        }
    }
}
```

---

## 6. ArkSqlDependencyFilterProcessor.cs

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Data.SqlClient;
using OpenTelemetry;
using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights.OpenTelemetry;

/// <summary>
/// Filters SQL dependencies to specific database connections.
/// Equivalent to SkipSqlDatabaseDependencyFilter from v2.x.
/// </summary>
public sealed class ArkSqlDependencyFilterProcessor : BaseProcessor<Activity>
{
    private readonly string? _dataSource;
    private readonly string? _database;
    private readonly bool _enabled;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ArkSqlDependencyFilterProcessor"/> class.
    /// </summary>
    /// <param name="sqlConnectionString">SQL connection string to filter.</param>
    public ArkSqlDependencyFilterProcessor(string? sqlConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(sqlConnectionString))
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlConnectionString);
                _dataSource = builder.DataSource;
                _database = builder.InitialCatalog;
                _enabled = !string.IsNullOrWhiteSpace(_dataSource) && 
                           !string.IsNullOrWhiteSpace(_database);
            }
            catch
            {
                _enabled = false;
            }
        }
    }
    
    /// <summary>
    /// Called when an Activity starts.
    /// </summary>
    public override void OnStart(Activity data)
    {
        if (!_enabled) return;
        
        var dbSystem = data.GetTagItem("db.system") as string;
        if (!"mssql".Equals(dbSystem, StringComparison.OrdinalIgnoreCase))
            return;
        
        var peerService = data.GetTagItem("peer.service") as string ?? 
                          data.GetTagItem("db.connection_string") as string;
        var dbName = data.GetTagItem("db.name") as string;
        
        if (!string.IsNullOrEmpty(peerService) && 
            peerService.Contains(_dataSource!, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(dbName) &&
            dbName.Equals(_database, StringComparison.Ordinal))
        {
            // Filter this SQL dependency
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            data.IsAllDataRequested = false;
        }
    }
}
```

---

## 7. ArkAdaptiveSamplerOptions.cs

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.ApplicationInsights.OpenTelemetry;

/// <summary>
/// Configuration options for <see cref="ArkAdaptiveSampler"/>.
/// </summary>
public sealed class ArkAdaptiveSamplerOptions
{
    /// <summary>
    /// Gets or sets the target number of traces per second.
    /// Default is 1.0.
    /// </summary>
    public double TracesPerSecond { get; set; } = 1.0;
    
    /// <summary>
    /// Gets or sets the moving average ratio for smoothing rate changes.
    /// Value between 0.0 and 1.0. Default is 0.5.
    /// </summary>
    public double MovingAverageRatio { get; set; } = 0.5;
    
    /// <summary>
    /// Gets or sets the timeout before decreasing sampling percentage.
    /// Default is 1 minute.
    /// </summary>
    public TimeSpan SamplingPercentageDecreaseTimeout { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// Gets or sets whether to enable per-operation bucketing.
    /// When true, each operation gets its own rate limit bucket.
    /// Default is true.
    /// </summary>
    public bool EnablePerOperationBucketing { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the maximum number of operation buckets to maintain.
    /// Prevents memory leaks from unbounded operation names.
    /// Default is 100.
    /// </summary>
    public int MaxOperationBuckets { get; set; } = 100;
    
    /// <summary>
    /// Validates the options.
    /// </summary>
    public void Validate()
    {
        if (TracesPerSecond <= 0)
            throw new ArgumentException("TracesPerSecond must be greater than 0", nameof(TracesPerSecond));
        
        if (MovingAverageRatio < 0 || MovingAverageRatio > 1)
            throw new ArgumentException("MovingAverageRatio must be between 0 and 1", nameof(MovingAverageRatio));
        
        if (SamplingPercentageDecreaseTimeout <= TimeSpan.Zero)
            throw new ArgumentException("SamplingPercentageDecreaseTimeout must be positive", nameof(SamplingPercentageDecreaseTimeout));
        
        if (MaxOperationBuckets <= 0)
            throw new ArgumentException("MaxOperationBuckets must be greater than 0", nameof(MaxOperationBuckets));
    }
}
```

---

## 8. ServiceCollectionExtensions.cs

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ark.Tools.ApplicationInsights.OpenTelemetry;

/// <summary>
/// Extension methods for configuring Ark.Tools OpenTelemetry customizations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ark.Tools OpenTelemetry customizations to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration for sampler options.</param>
    /// <param name="sqlConnectionStringToFilter">Optional SQL connection string to filter from telemetry.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddArkOpenTelemetryCustomizations(
        this IServiceCollection services,
        IConfiguration configuration,
        string? sqlConnectionStringToFilter = null)
    {
        // Configure sampler options
        services.Configure<ArkAdaptiveSamplerOptions>(o =>
        {
            o.TracesPerSecond = 1.0;
            o.MovingAverageRatio = 0.5;
            o.SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(1);
            o.EnablePerOperationBucketing = true;
            o.MaxOperationBuckets = 100;
        });
        
        // Bind from configuration
        services.Configure<ArkAdaptiveSamplerOptions>(
            configuration.GetSection("ApplicationInsights:ArkAdaptiveSampler"));
        
        // Validate options
        services.AddSingleton<IValidateOptions<ArkAdaptiveSamplerOptions>, 
            ArkAdaptiveSamplerOptionsValidator>();
        
        // Configure TelemetryConfiguration to use custom sampler and processors
        services.AddSingleton<IConfigureOptions<TelemetryConfiguration>>(sp =>
        {
            var samplerOptions = sp.GetRequiredService<IOptions<ArkAdaptiveSamplerOptions>>().Value;
            
            return new ConfigureNamedOptions<TelemetryConfiguration>(Options.DefaultName, tc =>
            {
                tc.ConfigureOpenTelemetryBuilder(builder =>
                {
                    // Set custom adaptive sampler
                    builder.SetSampler(new ArkAdaptiveSampler(samplerOptions));
                    
                    // Add processors in order
                    builder.AddProcessor(new ArkPreFilterProcessor());
                    builder.AddProcessor(new ArkTelemetryEnrichmentProcessor());
                    
                    if (!string.IsNullOrWhiteSpace(sqlConnectionStringToFilter))
                    {
                        builder.AddProcessor(new ArkSqlDependencyFilterProcessor(sqlConnectionStringToFilter));
                    }
                });
            });
        });
        
        return services;
    }
}

internal sealed class ArkAdaptiveSamplerOptionsValidator : IValidateOptions<ArkAdaptiveSamplerOptions>
{
    public ValidateOptionsResult Validate(string? name, ArkAdaptiveSamplerOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
```

---

## 9. Updated AspNetCore Startup Extension

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.ApplicationInsights.OpenTelemetry;
using Ark.Tools.NLog;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;

namespace Ark.Tools.AspNetCore.ApplicationInsights.Startup;

public static partial class Ex
{
    [RequiresUnreferencedCode("Application Insights configuration binding uses reflection. Configuration types and their properties may be trimmed.")]
    public static IServiceCollection ArkApplicationInsightsTelemetry(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Resolve connection string from configuration
        var connectionString = configuration["ApplicationInsights:ConnectionString"]
            ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        var instrumentationKey = configuration["ApplicationInsights:InstrumentationKey"]
            ?? configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
        
        var hasValidConnectionString = !string.IsNullOrWhiteSpace(connectionString) || 
                                       !string.IsNullOrWhiteSpace(instrumentationKey);
        
        // Check if we're in a test environment
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isIntegrationTests = string.Equals(environment, "IntegrationTests", StringComparison.OrdinalIgnoreCase);
        var useInMemoryChannel = isIntegrationTests || Debugger.IsAttached;
        
        // In test environments, use InMemoryChannel
        if (useInMemoryChannel || !hasValidConnectionString)
        {
#pragma warning disable CA2000 // DI container manages lifetime
            services.AddSingleton<Microsoft.ApplicationInsights.Channel.ITelemetryChannel>(
                new Microsoft.ApplicationInsights.Channel.InMemoryChannel { DeveloperMode = true });
#pragma warning restore CA2000
        }
        
        // Add Application Insights
        services.AddApplicationInsightsTelemetry(o =>
        {
            o.ConnectionString = hasValidConnectionString
                ? (connectionString ?? $"InstrumentationKey={instrumentationKey}")
                : "InstrumentationKey=00000000-0000-0000-0000-000000000000";
            
            // Disable built-in adaptive sampling (using custom sampler instead)
            o.EnableAdaptiveSampling = false;
            o.EnableHeartbeat = !useInMemoryChannel;
            o.AddAutoCollectedMetricExtractor = true;
            o.RequestCollectionOptions.InjectResponseHeaders = true;
            o.RequestCollectionOptions.TrackExceptions = true;
            o.DeveloperMode ??= isIntegrationTests || Debugger.IsAttached;
            o.EnableDebugLogger = Debugger.IsAttached || !hasValidConnectionString;
            o.ApplicationVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion;
        });
        
        // Configure dependency tracking
        services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) =>
        {
            module.EnableSqlCommandTextInstrumentation = true;
        });
        
        // Get SQL connection string for filtering (if configured)
        var sqlConnectionString = configuration.GetNLogSetting(
            "ConnectionStrings:" + NLogDefaultConfigKeys.SqlConnStringName);
        
        // Add custom OpenTelemetry sampler and processors
        services.AddArkOpenTelemetryCustomizations(configuration, sqlConnectionString);
        
        // Configure Snapshot Collector
        services.Configure<SnapshotCollectorConfiguration>(
            configuration.GetSection(nameof(SnapshotCollectorConfiguration)));
        services.AddSnapshotCollector();
        
        return services;
    }
}
```

---

## 10. Unit Test Examples

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.ApplicationInsights.OpenTelemetry;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights.Tests.OpenTelemetry;

[TestClass]
public class ArkAdaptiveSamplerTests
{
    [TestMethod]
    public void ShouldSample_AlwaysSamplesFailures()
    {
        // Arrange
        var options = new ArkAdaptiveSamplerOptions
        {
            TracesPerSecond = 0.001, // Very low rate to test failure preservation
            EnablePerOperationBucketing = false
        };
        var sampler = new ArkAdaptiveSampler(options);
        
        // Create activity with error status
        using var activity = new Activity("test-operation");
        activity.SetStatus(ActivityStatusCode.Error, "Test error");
        
        var parameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "test-operation",
            kind: ActivityKind.Server,
            tags: null,
            links: null);
        
        // Act
        var result = sampler.ShouldSample(parameters);
        
        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample, 
            "Failures must always be sampled regardless of rate limit");
    }
    
    [TestMethod]
    public void ShouldSample_RateLimitsSuccessfulRequests()
    {
        // Arrange
        var options = new ArkAdaptiveSamplerOptions
        {
            TracesPerSecond = 10, // 10 traces per second
            EnablePerOperationBucketing = false
        };
        var sampler = new ArkAdaptiveSampler(options);
        
        // Act - Generate 100 samples rapidly
        var sampledCount = 0;
        for (var i = 0; i < 100; i++)
        {
            var parameters = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "test-operation",
                kind: ActivityKind.Server,
                tags: null,
                links: null);
            
            var result = sampler.ShouldSample(parameters);
            if (result.Decision == SamplingDecision.RecordAndSample)
                sampledCount++;
        }
        
        // Assert - Should sample approximately 10 (with some burst allowance)
        sampledCount.Should().BeLessThan(30, 
            "Rate limiter should prevent excessive sampling");
    }
    
    [TestMethod]
    public void ShouldSample_PerOperationBucketing_DistributesFairly()
    {
        // Arrange
        var options = new ArkAdaptiveSamplerOptions
        {
            TracesPerSecond = 1, // 1 trace per second per operation
            EnablePerOperationBucketing = true
        };
        var sampler = new ArkAdaptiveSampler(options);
        
        var op1Sampled = 0;
        var op2Sampled = 0;
        
        // Act - Generate from two different operations
        for (var i = 0; i < 50; i++)
        {
            var params1 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "operation-1",
                kind: ActivityKind.Server,
                tags: null,
                links: null);
            
            if (sampler.ShouldSample(params1).Decision == SamplingDecision.RecordAndSample)
                op1Sampled++;
            
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "operation-2",
                kind: ActivityKind.Server,
                tags: null,
                links: null);
            
            if (sampler.ShouldSample(params2).Decision == SamplingDecision.RecordAndSample)
                op2Sampled++;
        }
        
        // Assert - Both operations should get samples (fair distribution)
        op1Sampled.Should().BeGreaterThan(0, "Operation 1 should get samples");
        op2Sampled.Should().BeGreaterThan(0, "Operation 2 should get samples");
        
        // They should be roughly equal (within reason given small sample size)
        Math.Abs(op1Sampled - op2Sampled).Should().BeLessThan(5,
            "Per-operation bucketing should distribute samples fairly");
    }
    
    [TestMethod]
    public void PreFilterProcessor_FiltersOptionsRequests()
    {
        // Arrange
        var processor = new ArkPreFilterProcessor();
        using var activity = new Activity("OPTIONS /api/users");
        activity.SetTag("http.request.method", "OPTIONS");
        activity.SetTag("http.response.status_code", 200);
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        
        // Act
        processor.OnStart(activity);
        
        // Assert
        activity.ActivityTraceFlags.Should().NotHaveFlag(ActivityTraceFlags.Recorded,
            "OPTIONS requests should be filtered out");
    }
}
```

---

## 11. Configuration Examples

### appsettings.json

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=https://...",
    "ArkAdaptiveSampler": {
      "TracesPerSecond": 1.0,
      "MovingAverageRatio": 0.5,
      "SamplingPercentageDecreaseTimeout": "00:01:00",
      "EnablePerOperationBucketing": true,
      "MaxOperationBuckets": 100
    }
  },
  "ConnectionStrings": {
    "NLog": "Server=...;Database=NLog;..."
  }
}
```

### Startup Configuration

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add Application Insights with Ark.Tools customizations
        services.ArkApplicationInsightsTelemetry(Configuration);
        
        // Other services...
    }
}
```

---

## Migration Checklist

### Code Changes

- [ ] Create `ArkAdaptiveSampler.cs`
- [ ] Create `OperationBucket.cs`
- [ ] Create `AdaptiveRateController.cs`
- [ ] Create `ArkPreFilterProcessor.cs`
- [ ] Create `ArkTelemetryEnrichmentProcessor.cs`
- [ ] Create `ArkSqlDependencyFilterProcessor.cs`
- [ ] Create `ArkAdaptiveSamplerOptions.cs`
- [ ] Create `ServiceCollectionExtensions.cs`
- [ ] Update `Ex.cs` in AspNetCore.ApplicationInsights
- [ ] Update `Ex.cs` in ApplicationInsights.HostedService
- [ ] Remove obsolete v2.x classes
- [ ] Update `Directory.Packages.props`

### Testing

- [ ] Unit tests for sampler
- [ ] Unit tests for processors
- [ ] Unit tests for token bucket
- [ ] Unit tests for adaptive controller
- [ ] Integration tests for end-to-end telemetry
- [ ] Load tests for rate limiting
- [ ] Performance benchmarks

### Documentation

- [ ] XML documentation on all public APIs
- [ ] Migration guide
- [ ] Architecture documentation
- [ ] Configuration reference
- [ ] Troubleshooting guide

### Deployment

- [ ] Feature flag for gradual rollout
- [ ] Monitoring dashboard
- [ ] Cost analysis queries
- [ ] Runbook for rollback
- [ ] Staged deployment plan

---

**Document Version:** 1.0  
**Date:** 2026-04-27  
**Author:** GitHub Copilot  
**Status:** Implementation Ready
