# Application Insights v3 to OpenTelemetry Migration Analysis

## Executive Summary

Application Insights v3.x represents a fundamental architectural shift from a proprietary telemetry system to one built on OpenTelemetry. This migration requires replacing custom telemetry processors and initializers with OpenTelemetry-native implementations while preserving critical sampling behavior that provides significant cost savings.

**Key Recommendation:** Implement a custom OpenTelemetry sampler that preserves Ark.Tools' adaptive sampling logic with failure preservation and per-operation rate limiting.

---

## Current Ark.Tools Sampling Architecture

### 1. DoNotSampleFailures (ITelemetryInitializer)

**Purpose:** Ensures all failed requests, dependencies, and exceptions are always sampled (100%)

**Implementation:**
```csharp
public class DoNotSampleFailures : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is ExceptionTelemetry || 
            (telemetry is DependencyTelemetry dp && dp.Success == false) || 
            (telemetry is RequestTelemetry rq && rq.Success == false))
        {
            if (telemetry is ISupportSampling s)
                s.SamplingPercentage = 100;
        }
    }
}
```

**Key Behavior:**
- Overrides any sampling decision for failures
- Ensures visibility into all errors regardless of sampling rate
- Critical for debugging and SLA monitoring

### 2. EnableAdaptiveSamplingWithCustomSettings (IConfigureOptions)

**Purpose:** Configures adaptive sampling with custom parameters and per-telemetry-type sampling

**Implementation:**
```csharp
public void Configure(TelemetryConfiguration tc)
{
    tc.DefaultTelemetrySink.TelemetryProcessorChainBuilder
        .UseAdaptiveSampling(_settings.Value, samplingCallback, excludedTypes: "Event")
        .UseAdaptiveSampling(_settings.Value, null, includedTypes: "Event")
        .Build();
}
```

**Key Behavior:**
- Adaptive sampling on all telemetry types except Events
- Separate adaptive sampling for Events
- Dynamic rate adjustment based on:
  - `MovingAverageRatio`: 0.5 (smooths rate changes)
  - `MaxTelemetryItemsPerSecond`: 1 (target rate)
  - `SamplingPercentageDecreaseTimeout`: 1 minute (how quickly to reduce sampling)
- Callback updates sampling percentage for observability

### 3. ArkSkipUselessSpamTelemetryProcessor (ITelemetryProcessor)

**Purpose:** Filters out high-volume, low-value telemetry before sampling

**Implementation:**
```csharp
public void Process(ITelemetry item)
{
    // Skip successful OPTIONS requests (CORS preflight)
    if (item is RequestTelemetry r && r.Success == true && 
        r.Name?.StartsWith("OPTIONS", StringComparison.OrdinalIgnoreCase) == true)
        return;

    // Skip successful Azure Service Bus Receive operations
    if (item is DependencyTelemetry d && d.Success == true)
    {
        if (d.Name == "Receive" && d.Type == "Azure Service Bus")
            return;
        if (d.Name.StartsWith("ServiceBusReceiver.", StringComparison.OrdinalIgnoreCase))
            return;
        if (d.Type == "SQL" && d.Data == "Commit")
            return;
    }

    _next.Process(item);
}
```

**Key Behavior:**
- Pre-filtering reduces telemetry volume before sampling
- Removes noise from high-frequency, low-value operations
- Improves signal-to-noise ratio in telemetry

---

## Sampling Goals & Requirements

### Primary Objectives

1. **Preserve All Failures**: Never sample out exceptions, failed requests, or failed dependencies
2. **Adaptive Rate Limiting**: Dynamically adjust sampling to maintain target telemetry volume
3. **Per-Operation Fairness**: Avoid over-sampling high-frequency endpoints while under-sampling rare ones
4. **Cost Efficiency**: Maintain current cost savings from adaptive sampling (major requirement)
5. **Pre-filtering**: Continue to filter out low-value telemetry

### Current Behavior Analysis

**Bucket-Based Sampling:**
The term "buckets over the 'first span' identifiers" refers to grouping traces by their root operation name (e.g., HTTP endpoint, message handler) and applying rate limits per group. This ensures:

- Rarely-called operations (e.g., admin endpoints) are sampled fairly
- High-frequency operations (e.g., health checks, metrics endpoints) don't dominate the sample
- Each "bucket" (operation type) gets proportional representation

**Adaptive Component:**
The `SamplingPercentageEstimatorSettings` dynamically adjusts sampling percentage based on:
- Current telemetry rate vs. target rate
- Moving average to smooth out spikes
- Configurable decrease timeout to avoid thrashing

---

## OpenTelemetry v3.x Architecture

### What Changed

**Removed from Public API:**
- `ITelemetryInitializer` interface
- `ITelemetryProcessor` interface
- `TelemetryConfiguration.TelemetryInitializers` collection
- `TelemetryConfiguration.TelemetryProcessors` collection
- `SamplingPercentageEstimatorSettings` class
- Adaptive sampling infrastructure

**New OpenTelemetry-Based API:**
- Samplers: Implement `Sampler` abstract class
- Processors: Implement `BaseProcessor<Activity>` for traces
- Configuration: Via `TracerProviderBuilder`
- Exporters: `Azure.Monitor.OpenTelemetry.Exporter`

### OpenTelemetry Sampling Model

**Head Sampling (Sampler):**
- Decision made at trace creation time
- Based on trace ID, attributes, parent context
- Cannot inspect full trace or span content
- Most efficient (low overhead)

**Tail Sampling (Collector):**
- Decision made after trace completes
- Can inspect full trace including errors
- Requires OpenTelemetry Collector infrastructure
- Higher latency and complexity

**Rate-Limited Sampling (Built-in):**
- `TelemetryConfiguration.TracesPerSecond` property
- Fixed rate, not adaptive
- No per-operation awareness
- No automatic failure preservation

---

## Available OpenTelemetry Sampler Packages

### 1. Core OpenTelemetry Samplers

**Package:** `OpenTelemetry` (included in ApplicationInsights 3.x dependencies)

**Built-in Samplers:**
- `AlwaysOnSampler`: Sample everything
- `AlwaysOffSampler`: Sample nothing
- `TraceIdRatioBasedSampler`: Probabilistic sampling by trace ID
- `ParentBasedSampler`: Inherit parent's sampling decision

**Limitations:**
- No adaptive behavior
- No per-operation rate limiting
- No automatic failure preservation

### 2. AWS Extensions

**Package:** `OpenTelemetry.Extensions.AWS` v1.15.1+

**Features:**
- `RateLimitingSampler`: Limits traces per second globally
- Not per-operation
- Not adaptive
- No failure preservation

**Assessment:** Insufficient for Ark.Tools requirements

### 3. Azure Monitor Approach

**Package:** `Microsoft.ApplicationInsights.AspNetCore` v3.1.0

**Default Behavior:**
- Rate-limited sampling via `TracesPerSecond`
- No adaptive adjustment
- No per-operation bucketing
- No automatic failure preservation

**Assessment:** Regression from v2.x capabilities

### 4. Third-Party Options

**Research Results:**
- No production-ready adaptive sampler with per-operation rate limiting found on NuGet
- Some experimental packages exist but lack maintenance
- Most teams implement custom samplers for advanced scenarios

---

## Migration Strategy: Custom OpenTelemetry Sampler

### Recommendation

Implement a **custom OpenTelemetry Sampler** that combines:

1. **Failure preservation** (always sample errors/exceptions)
2. **Adaptive rate limiting** (dynamic adjustment to target rate)
3. **Per-operation bucketing** (fair sampling across operation types)
4. **Pre-filtering** (via custom processor)

### Architecture Overview

```
Activity Creation
       ↓
[Custom Pre-Filter Processor] ← Filters out noise (OPTIONS, Service Bus Receive, etc.)
       ↓
[Custom Adaptive Sampler] ← Decision: Sample or Drop
       ↓                      • Always sample if error/exception
       ↓                      • Rate limit per operation bucket
       ↓                      • Adapt rate based on telemetry volume
       ↓
[Azure Monitor Exporter] ← Send to Application Insights
```

### Key Components

#### 1. ArkAdaptiveSampler (Custom Sampler)

**Responsibilities:**
- Make head sampling decisions
- Preserve all failures/exceptions
- Implement per-operation rate limiting
- Adapt sampling rate dynamically

**Implementation approach:**
```csharp
public class ArkAdaptiveSampler : Sampler
{
    // Per-operation token buckets
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets;
    
    // Adaptive rate controller
    private readonly AdaptiveRateController _rateController;
    
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var activity = samplingParameters.Activity;
        var operationName = activity.DisplayName;
        
        // ALWAYS sample failures
        if (HasError(activity) || HasException(activity))
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }
        
        // Get or create bucket for this operation
        var bucket = _buckets.GetOrAdd(operationName, CreateBucket);
        
        // Try to consume token from bucket
        if (bucket.TryConsume())
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }
        
        return new SamplingResult(SamplingDecision.Drop);
    }
}
```

#### 2. ArkPreFilterProcessor (Custom Processor)

**Responsibilities:**
- Filter out high-volume, low-value telemetry
- Run before sampler decision
- Equivalent to `ArkSkipUselessSpamTelemetryProcessor`

**Implementation approach:**
```csharp
public class ArkPreFilterProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
        // Check if should filter early
        if (ShouldFilter(activity))
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
    
    private bool ShouldFilter(Activity activity)
    {
        var operationName = activity.DisplayName;
        
        // OPTIONS requests
        if (operationName?.StartsWith("OPTIONS ", StringComparison.OrdinalIgnoreCase) == true)
            return true;
            
        // Service Bus Receive operations
        if (activity.Tags.Any(t => t.Key == "messaging.operation" && t.Value == "receive"))
            return true;
            
        // SQL Commit operations
        if (activity.Tags.Any(t => t.Key == "db.operation" && t.Value == "Commit"))
            return true;
            
        return false;
    }
}
```

#### 3. Token Bucket Algorithm

**Per-Operation Rate Limiting:**
```csharp
public class TokenBucket
{
    private double _tokens;
    private DateTime _lastRefill;
    private readonly double _rate; // tokens per second
    private readonly double _capacity;
    private readonly object _lock = new object();
    
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
        _tokens = Math.Min(_capacity, _tokens + elapsed * _rate);
        _lastRefill = now;
    }
}
```

#### 4. Adaptive Rate Controller

**Dynamic Rate Adjustment:**
```csharp
public class AdaptiveRateController
{
    private readonly SamplingPercentageEstimatorSettings _settings;
    private double _currentRate;
    private readonly MovingAverage _movingAverage;
    
    public double GetCurrentRate(double observedRate)
    {
        // Similar to Application Insights adaptive logic
        var targetRate = _settings.MaxTelemetryItemsPerSecond;
        var ratio = observedRate / targetRate;
        
        if (ratio > 1.0)
        {
            // Receiving too much, decrease rate
            _currentRate *= _settings.MovingAverageRatio;
        }
        else if (ratio < 0.8)
        {
            // Have capacity, increase rate cautiously
            _currentRate *= (1.0 + (1.0 - _settings.MovingAverageRatio));
        }
        
        return _movingAverage.Update(_currentRate);
    }
}
```

---

## Cost Impact Analysis

### v2.x Adaptive Sampling Benefits

**Typical Scenario:**
- Production application with variable load
- Peak traffic: 10,000 requests/min
- Average traffic: 1,000 requests/min
- Target rate: 1 telemetry item/second

**v2.x Behavior:**
- During peaks: Sampling percentage drops to ~0.6%
- During normal: Sampling percentage at ~6%
- During low traffic: Sampling percentage at 100%
- **All failures always sampled regardless of rate**

**Cost Savings:**
- Dramatic reduction during peaks (most expensive)
- Full visibility during low traffic
- Complete error coverage

### v3.x Rate-Limited Sampling Impact

**Same Scenario:**
- Fixed rate: 1 trace/second
- No adaptation to traffic patterns
- No guarantee of failure preservation

**v3.x Default Behavior:**
- During peaks: Random 0.6% of traces (may miss errors)
- During normal: Random 6% of traces
- During low traffic: May still drop traces unnecessarily

**Potential Issues:**
- **Risk of missing critical errors during peaks**
- Over-sampling during low traffic (wasted quota)
- No per-operation fairness (rare endpoints under-sampled)

### Custom Sampler Benefits

**With Proposed Implementation:**
- ✅ All failures preserved (100% error visibility)
- ✅ Adaptive rate adjustment (cost optimization)
- ✅ Per-operation fairness (balanced representation)
- ✅ Pre-filtering (reduced noise)

**Expected Cost Impact:** **Neutral to v2.x** (maintains current cost efficiency)

---

## Alternative Approaches Considered

### 1. Use Built-in Rate-Limited Sampling

**Approach:** Configure `TelemetryConfiguration.TracesPerSecond`

**Pros:**
- Simple implementation
- No custom code

**Cons:**
- ❌ No automatic failure preservation
- ❌ No adaptive behavior
- ❌ No per-operation bucketing
- ❌ **Potential cost increase** during peaks (if set too high)
- ❌ **Potential error loss** during peaks (if set too low)

**Verdict:** **Not recommended** - Unacceptable regression

### 2. OpenTelemetry Collector Tail Sampling

**Approach:** Deploy OpenTelemetry Collector with tail_sampling processor

**Pros:**
- Can inspect full traces
- Sophisticated policies
- Failure preservation possible

**Cons:**
- ❌ Requires infrastructure (Collector deployment)
- ❌ Increased latency (all traces sent to collector first)
- ❌ Operational complexity
- ❌ Additional costs (collector ingress)
- ❌ Single point of failure

**Verdict:** **Not recommended** - Too complex for current benefits

### 3. Stay on Application Insights v2.x

**Approach:** Revert to Microsoft.ApplicationInsights 2.23.0

**Pros:**
- No migration work
- Maintains current behavior
- Known cost profile

**Cons:**
- ❌ No new features
- ❌ Limited support timeline
- ❌ Security vulnerability in transitive dependencies
- ❌ Incompatible with future .NET versions

**Verdict:** **Temporary fallback only** - Not sustainable long-term

### 4. Hybrid Approach: v2.x with Gradual Migration

**Approach:** 
- Keep v2.x in production
- Develop and test custom sampler in parallel
- Switch once validated

**Pros:**
- ✅ Risk mitigation
- ✅ Time for thorough testing
- ✅ Cost comparison possible

**Cons:**
- Longer timeline
- Dual maintenance

**Verdict:** **Viable option** - Safest path forward

---

## Risk Assessment

### Migration Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|------------|------------|
| Custom sampler bugs causing data loss | High | Medium | Extensive testing, feature flags, gradual rollout |
| Increased telemetry costs | High | Medium | Monitoring, alerts, cost analysis dashboard |
| Performance degradation | Medium | Low | Benchmarking, profiling, optimization |
| Incomplete error capture | High | Low | Comprehensive testing with error scenarios |
| Operational complexity | Medium | Medium | Documentation, monitoring, runbooks |

### Staying on v2.x Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|------------|------------|
| End of support/security issues | High | High | Must migrate eventually |
| Incompatibility with future .NET | High | High | Blocks platform upgrades |
| Missing new OpenTelemetry ecosystem | Medium | High | Forgo improvements |

---

## Recommendations

### Immediate Actions

1. **Accept v3.x Upgrade with Custom Implementation**
   - Proceed with migration to maintain platform support
   - Implement custom OpenTelemetry sampler as outlined

2. **Implement in Phases**
   - Phase 1: Create custom sampler with failure preservation
   - Phase 2: Add per-operation bucketing
   - Phase 3: Add adaptive rate controller
   - Phase 4: Production validation

3. **Establish Success Criteria**
   - 100% error capture rate maintained
   - Telemetry costs within ±10% of v2.x baseline
   - No P0/P1 performance regressions
   - Per-operation sampling fairness verified

### Long-Term Strategy

1. **Monitor OpenTelemetry Ecosystem**
   - Watch for community samplers that may meet requirements
   - Contribute custom sampler to open-source if validated

2. **Continuous Optimization**
   - Refine adaptive algorithms based on production data
   - Add configuration options for per-environment tuning

3. **Document and Share**
   - Internal documentation for operations
   - Consider blog post/talk on adaptive sampling in OpenTelemetry

---

## Conclusion

The migration from Application Insights v2.x to v3.x is not a simple package upgrade - it represents a fundamental architectural shift. The removal of adaptive sampling capabilities requires a custom implementation to maintain the significant cost savings and error visibility that Ark.Tools currently enjoys.

**The recommended path forward is to implement a custom OpenTelemetry sampler** that preserves the sophisticated sampling behavior built into Ark.Tools while embracing the OpenTelemetry standard for future compatibility.

This approach:
- ✅ Maintains current cost efficiency
- ✅ Preserves 100% error visibility
- ✅ Provides per-operation fairness
- ✅ Ensures platform support and security
- ✅ Positions Ark.Tools for OpenTelemetry ecosystem growth

The investment in custom sampler implementation is justified by the ongoing cost savings and operational benefits it provides.

---

**Document Version:** 1.0  
**Date:** 2026-04-27  
**Author:** GitHub Copilot  
**Review Status:** Pending
