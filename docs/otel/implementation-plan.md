# OpenTelemetry Migration Implementation Plan

## Overview

This document provides a detailed implementation plan for migrating Ark.Tools.ApplicationInsights from Application Insights v2.x to v3.x (OpenTelemetry-based) while preserving adaptive sampling, failure preservation, and per-operation rate limiting capabilities.

---

## Phase 1: Infrastructure & Core Components

### 1.1 Create ArkAdaptiveSampler

**File:** `src/common/Ark.Tools.ApplicationInsights/OpenTelemetry/ArkAdaptiveSampler.cs`

**Requirements:**
- Extend OpenTelemetry `Sampler` abstract class
- Implement `ShouldSample` method
- Support failure preservation
- Support per-operation token buckets
- Thread-safe implementation

**Key Methods:**
```csharp
public class ArkAdaptiveSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters);
    public override string Description { get; }
}
```

**Configuration Class:**
```csharp
public class ArkAdaptiveSamplerOptions
{
    public double TracesPerSecond { get; set; } = 1.0;
    public double MovingAverageRatio { get; set; } = 0.5;
    public TimeSpan SamplingPercentageDecreaseTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public bool EnablePerOperationBucketing { get; set; } = true;
    public int MaxOperationBuckets { get; set; } = 100;
}
```

**Estimated Effort:** 2-3 days

### 1.2 Create Token Bucket Implementation

**File:** `src/common/Ark.Tools.ApplicationInsights/OpenTelemetry/TokenBucket.cs`

**Requirements:**
- Rate limiting per bucket
- Thread-safe token consumption
- Efficient refill algorithm
- Low memory footprint

**Key Methods:**
```csharp
public class TokenBucket
{
    public TokenBucket(double rate, double capacity);
    public bool TryConsume();
    private void Refill();
}
```

**Estimated Effort:** 1 day

### 1.3 Create Adaptive Rate Controller

**File:** `src/common/Ark.Tools.ApplicationInsights/OpenTelemetry/AdaptiveRateController.cs`

**Requirements:**
- Monitor telemetry rate
- Calculate adaptive sampling rate
- Moving average smoothing
- Configurable parameters

**Key Methods:**
```csharp
public class AdaptiveRateController
{
    public AdaptiveRateController(ArkAdaptiveSamplerOptions options);
    public double GetTargetRate(double observedRate);
    public void UpdateMetrics(int sampledCount, int droppedCount);
}
```

**Estimated Effort:** 2 days

---

## Phase 2: Activity Processors

### 2.1 Create ArkPreFilterProcessor

**File:** `src/common/Ark.Tools.ApplicationInsights/OpenTelemetry/ArkPreFilterProcessor.cs`

**Requirements:**
- Filter out low-value telemetry
- Equivalent to `ArkSkipUselessSpamTelemetryProcessor`
- Mark activities as not recorded to skip export

**Implementation:**
```csharp
public class ArkPreFilterProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
        if (ShouldFilterActivity(activity))
        {
            // Mark as not recorded
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
    
    private bool ShouldFilterActivity(Activity activity)
    {
        // Port logic from ArkSkipUselessSpamTelemetryProcessor
        // - OPTIONS requests
        // - Service Bus Receive operations
        // - SQL Commit operations
    }
}
```

**Filters to Implement:**
- OPTIONS requests (CORS preflight)
- Azure Service Bus Receive operations
- SQL Commit operations

**Estimated Effort:** 1-2 days

### 2.2 Create ArkTelemetryEnrichmentProcessor

**File:** `src/common/Ark.Tools.ApplicationInsights/OpenTelemetry/ArkTelemetryEnrichmentProcessor.cs`

**Requirements:**
- Add global properties to activities
- Equivalent to `GlobalInfoTelemetryInitializer`
- Set process name and other global attributes

**Implementation:**
```csharp
public class ArkTelemetryEnrichmentProcessor : BaseProcessor<Activity>
{
    private readonly string? _processName;
    
    public ArkTelemetryEnrichmentProcessor()
    {
        _processName = Assembly.GetEntryAssembly()?.GetName().Name;
    }
    
    public override void OnStart(Activity activity)
    {
        if (_processName != null && activity.GetTagItem("ProcessName") == null)
        {
            activity.SetTag("ProcessName", _processName);
        }
    }
}
```

**Estimated Effort:** 1 day

---

## Phase 3: Configuration & Integration

### 3.1 Create OpenTelemetry Configuration Extension

**File:** `src/common/Ark.Tools.ApplicationInsights/OpenTelemetry/ServiceCollectionExtensions.cs`

**Requirements:**
- Configure OpenTelemetry via ApplicationInsights 3.x API
- Use `TelemetryConfiguration.ConfigureOpenTelemetryBuilder`
- Register custom sampler and processors
- Maintain backward compatibility with existing configuration

**Implementation:**
```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArkOpenTelemetryCustomizations(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.Configure<TelemetryConfiguration>(tc =>
        {
            tc.ConfigureOpenTelemetryBuilder(builder =>
            {
                // Add custom sampler
                builder.SetSampler(new ArkAdaptiveSampler(samplerOptions));
                
                // Add custom processors
                builder.AddProcessor(new ArkPreFilterProcessor());
                builder.AddProcessor(new ArkTelemetryEnrichmentProcessor());
            });
        });
        
        return services;
    }
}
```

**Estimated Effort:** 2 days

### 3.2 Update AspNetCore Startup Extension

**File:** `src/aspnetcore/Ark.Tools.AspNetCore.ApplicationInsights/Startup/Ex.cs`

**Changes Required:**
1. Remove `AddApplicationInsightsTelemetryProcessor<>` calls
2. Remove `AddSingleton<ITelemetryInitializer>` calls
3. Remove `EnableAdaptiveSamplingWithCustomSettings` registration
4. Add new OpenTelemetry configuration
5. Update SQL dependency filtering approach

**Before (v2.x):**
```csharp
services.AddApplicationInsightsTelemetryProcessor<ArkSkipUselessSpamTelemetryProcessor>();
services.AddSingleton<ITelemetryInitializer, GlobalInfoTelemetryInitializer>();
services.AddSingleton<ITelemetryInitializer, DoNotSampleFailures>();
services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, EnableAdaptiveSamplingWithCustomSettings>();
```

**After (v3.x):**
```csharp
services.AddArkOpenTelemetryCustomizations(configuration);
```

**Estimated Effort:** 1-2 days

### 3.3 Update HostedService Extension

**File:** `src/common/Ark.Tools.ApplicationInsights.HostedService/Ex.cs`

**Changes Required:**
- Similar changes to AspNetCore extension
- Ensure WorkerService compatibility

**Estimated Effort:** 1 day

---

## Phase 4: Remove Obsolete Code

### 4.1 Files to Delete

After migration complete and validated:

- `src/common/Ark.Tools.ApplicationInsights/DoNotSampleFailures.cs` (replaced by sampler)
- `src/common/Ark.Tools.ApplicationInsights/EnableAdaptiveSamplingWithCustomSettings.cs` (replaced by sampler)
- `src/common/Ark.Tools.ApplicationInsights/ArkSkipUselessSpamTelemetryProcessor.cs` (replaced by processor)
- `src/common/Ark.Tools.ApplicationInsights/GlobalInfoTelemetryInitializer.cs` (replaced by processor)
- `src/common/Ark.Tools.ApplicationInsights/SkipSqlDatabaseDependencyFilter.cs` (replaced by processor)
- `src/*/SkipSqlDatabaseDependencyFilterFactory.cs` (replaced by processor)

**Estimated Effort:** 0.5 day

### 4.2 Package Dependencies to Remove

After migration:
- `Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel` (obsolete in v3)

---

## Phase 5: Testing & Validation

### 5.1 Unit Tests

**File:** `tests/Ark.Tools.ApplicationInsights.Tests/OpenTelemetry/ArkAdaptiveSamplerTests.cs`

**Test Scenarios:**
- Failures are always sampled (100%)
- Successful operations are rate-limited
- Per-operation bucketing works correctly
- Adaptive rate adjustment behaves correctly
- Thread safety under concurrent load
- Edge cases (null operations, missing tags, etc.)

**Estimated Effort:** 2-3 days

### 5.2 Integration Tests

**Test Scenarios:**
- Verify telemetry reaches Application Insights
- Validate sampling ratios in real scenarios
- Confirm failure preservation end-to-end
- Test with high load simulation
- Verify per-operation distribution

**Estimated Effort:** 2-3 days

### 5.3 Load Testing

**Requirements:**
- Simulate production-like traffic patterns
- Measure sampling behavior under load
- Verify cost profile matches expectations
- Identify performance bottlenecks

**Tools:**
- k6, JMeter, or similar
- Application Insights query analytics

**Estimated Effort:** 2 days

### 5.4 Production Validation Plan

**Staged Rollout:**
1. **Canary (5% traffic):** Deploy to small subset, monitor for 1 week
2. **Limited (25% traffic):** Expand if successful, monitor for 1 week
3. **Wide (75% traffic):** Further expansion, monitor for 1 week
4. **Full (100% traffic):** Complete rollout

**Monitoring:**
- Sampling rate metrics
- Error capture rate (should be 100%)
- Telemetry costs
- Application performance
- Error budgets/SLOs

**Rollback Criteria:**
- Error capture rate < 99%
- Cost increase > 20%
- Performance degradation > 5%
- Any P0/P1 incidents related to telemetry

**Estimated Effort:** 4 weeks (calendar time with monitoring periods)

---

## Implementation Timeline

### Sprint 1-2: Core Infrastructure (3-4 weeks)
- Week 1-2: Implement ArkAdaptiveSampler with basic rate limiting
- Week 3: Implement Token Bucket and per-operation bucketing
- Week 4: Implement Adaptive Rate Controller

### Sprint 3: Processors & Integration (2 weeks)
- Week 5: Implement ArkPreFilterProcessor and ArkTelemetryEnrichmentProcessor
- Week 6: Update startup extensions and configuration

### Sprint 4-5: Testing (3-4 weeks)
- Week 7-8: Unit and integration tests
- Week 9: Load testing and optimization
- Week 10: Documentation and preparation for production

### Sprint 6-9: Production Rollout (4 weeks + monitoring)
- Week 11: Canary deployment (5%)
- Week 12: Limited deployment (25%)
- Week 13: Wide deployment (75%)
- Week 14: Full deployment (100%)

**Total Estimated Timeline:** 14 weeks (3.5 months)

**Total Estimated Effort:** 25-30 developer days

---

## Technical Implementation Details

### Sampler Decision Logic

```csharp
public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
{
    var activity = samplingParameters.ParentContext.Activity ?? samplingParameters.Activity;
    
    // Step 1: Check for errors/exceptions (ALWAYS SAMPLE)
    if (IsFailure(activity))
    {
        RecordMetric("sampled", "reason:failure");
        return new SamplingResult(
            SamplingDecision.RecordAndSample,
            CreateSamplingAttributes(100.0));
    }
    
    // Step 2: Get operation name for bucketing
    var operationName = GetOperationName(activity);
    
    // Step 3: Get or create token bucket for this operation
    var bucket = GetOrCreateBucket(operationName);
    
    // Step 4: Try to consume token
    if (bucket.TryConsume())
    {
        RecordMetric("sampled", $"reason:rate_limit,operation:{operationName}");
        return new SamplingResult(
            SamplingDecision.RecordAndSample,
            CreateSamplingAttributes(bucket.CurrentSamplingPercentage));
    }
    
    // Step 5: Drop
    RecordMetric("dropped", $"reason:rate_limit,operation:{operationName}");
    return new SamplingResult(SamplingDecision.Drop);
}

private static bool IsFailure(Activity? activity)
{
    if (activity == null) return false;
    
    // Check status code
    if (activity.Status == ActivityStatusCode.Error)
        return true;
    
    // Check for exception events
    if (activity.Events.Any(e => e.Name == "exception"))
        return true;
    
    // Check HTTP status code
    if (activity.GetTagItem("http.response.status_code") is int statusCode)
    {
        if (statusCode >= 400)
            return true;
    }
    
    // Check RPC status
    if (activity.GetTagItem("rpc.grpc.status_code") is int grpcStatus)
    {
        if (grpcStatus != 0) // 0 = OK
            return true;
    }
    
    return false;
}

private static string GetOperationName(Activity? activity)
{
    if (activity == null) return "unknown";
    
    // Use operation name (usually HTTP route or method name)
    return activity.DisplayName ?? activity.OperationName ?? "unknown";
}
```

### Pre-Filter Processor Logic

```csharp
public class ArkPreFilterProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
        if (ShouldFilter(activity))
        {
            // Mark as not recorded to skip export entirely
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            activity.IsAllDataRequested = false;
        }
    }
    
    private static bool ShouldFilter(Activity activity)
    {
        var displayName = activity.DisplayName;
        var operationName = activity.OperationName;
        
        // Filter OPTIONS requests
        if (displayName?.Contains("OPTIONS", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        
        // Filter based on semantic conventions
        var httpMethod = activity.GetTagItem("http.request.method") as string;
        if ("OPTIONS".Equals(httpMethod, StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Filter Azure Service Bus Receive operations
        var messagingOperation = activity.GetTagItem("messaging.operation") as string;
        var messagingSystem = activity.GetTagItem("messaging.system") as string;
        if ("receive".Equals(messagingOperation, StringComparison.OrdinalIgnoreCase) &&
            "servicebus".Equals(messagingSystem, StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Filter SQL Commit operations
        var dbOperation = activity.GetTagItem("db.operation") as string;
        var dbSystem = activity.GetTagItem("db.system") as string;
        if ("Commit".Equals(dbOperation, StringComparison.OrdinalIgnoreCase) &&
            "mssql".Equals(dbSystem, StringComparison.OrdinalIgnoreCase))
            return true;
        
        return false;
    }
}
```

### SQL Dependency Filtering

**Challenge:** `SkipSqlDatabaseDependencyFilter` filtered specific database connections.

**OpenTelemetry Approach:**
```csharp
public class ArkSqlDependencyFilterProcessor : BaseProcessor<Activity>
{
    private readonly string _dataSource;
    private readonly string _database;
    private readonly bool _enabled;
    
    public ArkSqlDependencyFilterProcessor(string? connectionString)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            _dataSource = builder.DataSource;
            _database = builder.InitialCatalog;
            _enabled = !string.IsNullOrWhiteSpace(_dataSource) && 
                       !string.IsNullOrWhiteSpace(_database);
        }
    }
    
    public override void OnStart(Activity activity)
    {
        if (!_enabled) return;
        
        var dbSystem = activity.GetTagItem("db.system") as string;
        var peerService = activity.GetTagItem("peer.service") as string;
        var dbName = activity.GetTagItem("db.name") as string;
        
        if ("mssql".Equals(dbSystem, StringComparison.OrdinalIgnoreCase))
        {
            if (peerService?.Contains(_dataSource, StringComparison.Ordinal) == true &&
                dbName?.Equals(_database, StringComparison.Ordinal) == true)
            {
                // Filter this SQL dependency
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                activity.IsAllDataRequested = false;
            }
        }
    }
}
```

**Estimated Effort:** 1-2 days

---

## Phase 6: Configuration Migration

### Configuration Schema Changes

**v2.x Configuration:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "...",
    "EstimatorSettings": {
      "MovingAverageRatio": 0.5,
      "MaxTelemetryItemsPerSecond": 1,
      "SamplingPercentageDecreaseTimeout": "00:01:00"
    }
  }
}
```

**v3.x Configuration:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "...",
    "ArkAdaptiveSampler": {
      "TracesPerSecond": 1.0,
      "MovingAverageRatio": 0.5,
      "SamplingPercentageDecreaseTimeout": "00:01:00",
      "EnablePerOperationBucketing": true,
      "MaxOperationBuckets": 100
    }
  }
}
```

### Code Changes

**Old (v2.x):**
```csharp
services.Configure<SamplingPercentageEstimatorSettings>(o =>
{
    o.MovingAverageRatio = 0.5;
    o.MaxTelemetryItemsPerSecond = 1;
    o.SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(1);
});

services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, 
    EnableAdaptiveSamplingWithCustomSettings>();
```

**New (v3.x):**
```csharp
services.Configure<ArkAdaptiveSamplerOptions>(o =>
{
    o.TracesPerSecond = 1.0;
    o.MovingAverageRatio = 0.5;
    o.SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(1);
    o.EnablePerOperationBucketing = true;
});

services.Configure<ArkAdaptiveSamplerOptions>(o =>
{
    configuration.GetSection("ApplicationInsights:ArkAdaptiveSampler").Bind(o);
});

services.AddArkOpenTelemetryCustomizations(configuration);
```

**Estimated Effort:** 1 day

---

## Phase 7: Testing Strategy

### Unit Test Coverage

**File:** `tests/Ark.Tools.ApplicationInsights.Tests/OpenTelemetry/ArkAdaptiveSamplerTests.cs`

**Test Cases:**

1. **Failure Preservation**
   - Test: Create activity with error status → Verify sampled
   - Test: Create activity with exception event → Verify sampled
   - Test: Create activity with HTTP 500 → Verify sampled
   - Test: Create activity with HTTP 404 → Verify sampled

2. **Rate Limiting**
   - Test: Generate 100 activities at 10/sec with 1/sec limit → Verify ~10 sampled
   - Test: Generate activities under rate limit → Verify all sampled
   - Test: Generate burst then pause → Verify bucket refills

3. **Per-Operation Bucketing**
   - Test: Generate 50 from Op1, 50 from Op2 with 1/sec limit → Verify fair distribution
   - Test: Verify rare operations get sampled fairly
   - Test: Verify high-frequency operations don't dominate

4. **Adaptive Behavior**
   - Test: Increase traffic → Verify rate adjusts down
   - Test: Decrease traffic → Verify rate adjusts up
   - Test: Spike then normalize → Verify smooth adaptation

5. **Thread Safety**
   - Test: Concurrent sampling from multiple threads
   - Test: No race conditions in bucket access
   - Test: No deadlocks

**Estimated Effort:** 3 days

### Integration Test Coverage

**Test Scenarios:**

1. **End-to-End Telemetry**
   - Start application with OpenTelemetry configuration
   - Generate mix of successful and failed requests
   - Query Application Insights
   - Verify: All failures present, successful requests sampled at expected rate

2. **Configuration Binding**
   - Test various configuration scenarios
   - Verify options loaded correctly
   - Test default values

3. **Pre-Filter Processor**
   - Generate OPTIONS requests → Verify not exported
   - Generate Service Bus receives → Verify not exported
   - Generate SQL commits → Verify not exported

4. **SQL Dependency Filtering**
   - Configure SQL connection string filtering
   - Generate SQL dependencies to filtered database
   - Verify filtered correctly

**Estimated Effort:** 2-3 days

### Load Testing

**Scenarios:**

1. **Steady State**
   - 1000 req/min for 10 minutes
   - Verify sampling rate stabilizes around target

2. **Spike Traffic**
   - Baseline 100 req/min
   - Spike to 5000 req/min for 2 minutes
   - Return to baseline
   - Verify adaptive adjustment works

3. **Mixed Operations**
   - 10 different operation types
   - Different frequencies per operation
   - Verify fair sampling distribution

4. **Failure Scenarios**
   - Generate 10% failure rate
   - Verify all failures captured
   - Verify successful requests still rate-limited

**Metrics to Collect:**
- Actual vs. target sampling rate
- Per-operation sample counts
- Error capture rate (target: 100%)
- CPU/Memory overhead
- End-to-end latency impact

**Estimated Effort:** 2-3 days

---

## Phase 8: Documentation

### 8.1 Migration Guide

**File:** `docs/otel/migration-guide.md`

**Content:**
- Step-by-step upgrade instructions
- Configuration changes required
- Breaking changes and how to address them
- Troubleshooting common issues

**Estimated Effort:** 1 day

### 8.2 Architecture Documentation

**File:** `docs/otel/architecture.md`

**Content:**
- OpenTelemetry pipeline overview
- Custom sampler design
- Processor chain explanation
- Performance characteristics
- Monitoring and observability

**Estimated Effort:** 1 day

### 8.3 API Documentation

**Requirements:**
- XML documentation on all public classes
- Code examples
- Configuration reference

**Estimated Effort:** 1 day

---

## Dependencies & Prerequisites

### Required Packages

```xml
<PackageVersion Include="Microsoft.ApplicationInsights" Version="3.1.0" />
<PackageVersion Include="Microsoft.ApplicationInsights.AspNetCore" Version="3.1.0" />
<PackageVersion Include="Microsoft.ApplicationInsights.WorkerService" Version="3.1.0" />
<PackageVersion Include="Microsoft.ApplicationInsights.NLogTarget" Version="3.1.0-beta4" />
<PackageVersion Include="OpenTelemetry.Api" Version="1.15.3" />
<PackageVersion Include="OpenTelemetry.Extensions.Propagators" Version="1.15.3" />
```

### Development Environment

- .NET SDK 10.0.201+
- Visual Studio 2025 or Rider 2025
- Docker (for integration tests with SQL/ServiceBus/Azurite)

### Skills Required

- Deep understanding of OpenTelemetry concepts
- Experience with .NET diagnostics and Activity API
- Understanding of sampling algorithms and statistics
- Application Insights/Azure Monitor knowledge

---

## Success Criteria

### Functional Requirements

- ✅ All failures/exceptions are sampled (100% capture rate)
- ✅ Successful requests are rate-limited adaptively
- ✅ Per-operation bucketing provides fair sampling
- ✅ Pre-filtering removes low-value telemetry
- ✅ Configuration backward-compatible where possible
- ✅ Existing tests continue to pass

### Non-Functional Requirements

- ✅ Performance overhead < 1ms p99 per request
- ✅ Memory overhead < 10MB for sampling infrastructure
- ✅ Telemetry costs within ±10% of v2.x baseline
- ✅ No P0/P1 incidents during rollout
- ✅ Code coverage > 90% for sampling components

### Observability Requirements

- ✅ Metrics for sampling rate per operation
- ✅ Metrics for drop rate
- ✅ Metrics for error capture rate
- ✅ Logs for sampling decision audit trail (debug mode)
- ✅ Dashboard for sampling behavior monitoring

---

## Risk Mitigation Strategies

### Technical Risks

**Risk: Sampler bugs cause data loss**
- Mitigation: Extensive testing, feature flags, canary deployment
- Fallback: Quick rollback to v2.x prepared

**Risk: Performance degradation**
- Mitigation: Profiling, benchmarking, optimization passes
- Fallback: Disable per-operation bucketing if needed

**Risk: Increased costs**
- Mitigation: Cost monitoring, automatic alerts, staged rollout
- Fallback: Adjust sampling rates, revert if excessive

### Operational Risks

**Risk: Complex troubleshooting**
- Mitigation: Comprehensive documentation, runbooks, training
- Fallback: Expert on-call rotation

**Risk: Configuration errors**
- Mitigation: Validation, schema enforcement, sensible defaults
- Fallback: Fail-safe to v2.x behavior

---

## Alternative: Defer Migration

### If Custom Implementation is Too Complex

**Option:** Revert to Application Insights v2.23.0 and defer migration

**Steps:**
1. Revert `Directory.Packages.props` changes
2. Stay on v2.x for 6-12 months
3. Monitor Microsoft's roadmap for:
   - Community adaptive samplers
   - Azure Monitor improvements
   - Better migration tools

**Trade-offs:**
- ✅ No immediate work required
- ✅ Known stable behavior
- ❌ Limited support timeline
- ❌ Security vulnerabilities in transitive deps
- ❌ Blocks .NET platform upgrades
- ❌ Miss OpenTelemetry ecosystem benefits

**Assessment:** Only viable as **temporary measure** while building custom solution

---

## Conclusion

The migration to Application Insights v3.x requires significant engineering investment to preserve Ark.Tools' sophisticated sampling behavior. However, this investment is justified by:

1. **Long-term platform support** and security
2. **Cost savings preservation** (adaptive sampling)
3. **Operational excellence** (error visibility)
4. **Future compatibility** with OpenTelemetry ecosystem

The recommended approach is to **proceed with custom OpenTelemetry sampler implementation** following the phased plan outlined above.

---

## Appendix: Code Examples

### Complete Sampler Skeleton

See `implementation-plan-code-examples.md` for complete, runnable code examples.

### Performance Considerations

**Token Bucket Optimization:**
- Use `ConcurrentDictionary` with lazy initialization
- Limit max buckets to prevent memory leaks
- Implement LRU eviction for inactive buckets

**Adaptive Controller Optimization:**
- Update rate calculations periodically (not per-sample)
- Use background task for rate adjustment
- Minimize lock contention

### Monitoring Queries

**Application Insights Queries:**

```kusto
// Error capture rate
traces
| where timestamp > ago(1h)
| summarize 
    Total = count(),
    Errors = countif(severityLevel >= 3)
| extend ErrorCaptureRate = Errors * 100.0 / Total

// Sampling rate per operation
requests
| where timestamp > ago(1h)
| summarize 
    Count = count(),
    SampledRate = avg(todouble(customDimensions["SamplingPercentage"]))
    by name
| order by Count desc
```

---

**Document Version:** 1.0  
**Date:** 2026-04-27  
**Author:** GitHub Copilot  
**Status:** Implementation Ready
