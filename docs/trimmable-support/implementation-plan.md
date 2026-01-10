# Trimming Implementation Plan

**Last Updated:** 2026-01-10  
**Status:** Phase 1 - In Progress

## Executive Summary

This document outlines the strategy for making all 42 Ark.Tools common libraries trim-compatible. The work is divided into 5 phases spanning approximately 12 weeks.

## Goals

1. **Enable trimming** for all libraries where feasible
2. **Reduce deployment sizes** by 30-40% for trimmed applications
3. **Maintain compatibility** with existing code
4. **Establish patterns** for trim-safe code in future development

## Implementation Strategy

### Phase-Based Approach

The implementation follows a dependency-ordered approach, starting with libraries that have no Ark.Tools dependencies and working up the dependency tree.

### Success Criteria

For each library to be marked as trimmable:
- ✅ Build succeeds with `<IsTrimmable>true</IsTrimmable>`
- ✅ Build succeeds with `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`
- ✅ Zero IL#### trim warnings
- ✅ All existing tests pass
- ✅ Roundtrip/integration tests added for trim-sensitive code

## 5-Phase Rollout Plan

### Phase 1: Foundation Libraries (Weeks 1-2) 🔄 CURRENT

**Goal:** Mark the simplest libraries as trimmable and establish patterns

**Libraries:**
1. ✅ **Ark.Tools.Nodatime** - COMPLETED
   - Generic base class pattern established
   - Test coverage added
2. ✅ **Ark.Tools.Sql** - COMPLETED
3. ✅ **Ark.Tools.Outbox** - COMPLETED
4. ✅ **Ark.Tools.ApplicationInsights** - COMPLETED
   - Zero trim warnings (ApplicationInsights SDK fully compatible)
5. ⏳ **Ark.Tools.Core** - Critical blocker (9 warning types)

**Deliverables:**
- [x] Pattern for generic base classes
- [x] Test project template
- [ ] Documentation on handling IL2026 warnings
- [ ] Core library trim analysis

### Phase 2: Serialization Libraries (Weeks 3-4)

**Goal:** Enable trimming for JSON serialization libraries

**Libraries:**
1. Ark.Tools.Nodatime.SystemTextJson (requires source generators)
2. Ark.Tools.Nodatime.Json (Newtonsoft.Json patterns)
3. Ark.Tools.Nodatime.Dapper (type handler patterns)
4. Ark.Tools.SystemTextJson
5. Ark.Tools.NewtonsoftJson

**Key Challenges:**
- IL2026 warnings from JsonSerializer
- Newtonsoft.Json reflection requirements
- Dapper TypeDescriptor usage

**Approach:**
- Implement JSON source generators where applicable
- Use explicit type registration for Dapper handlers
- Consider splitting libraries if needed

### Phase 3: Infrastructure Libraries (Weeks 5-6)

**Goal:** Enable core infrastructure libraries

**Libraries:**
1. **Ark.Tools.NLog** ⚠️ HIGH PRIORITY (blocks ~20 libraries)
2. Ark.Tools.SimpleInjector (DI patterns)
3. Ark.Tools.Authorization
4. Ark.Tools.Hosting (Azure integration)

**Key Challenges:**
- NLog reflection patterns
- SimpleInjector container registration
- Azure SDK trim compatibility

### Phase 4: Integration Libraries (Weeks 7-10)

**Goal:** Enable integration and specialized libraries

**Libraries:**
1. Ark.Tools.Http (Flurl client)
2. Ark.Tools.FtpClient.* (5 implementations)
3. Ark.Tools.Sql.Oracle
4. Ark.Tools.Sql.SqlServer
5. Ark.Tools.Solid (+ 3 variants)
6. Ark.Tools.Auth0

**Approach:**
- Test each integration library independently
- Document third-party library constraints
- Split libraries if integration-specific code prevents trimming

### Phase 5: Complex Frameworks (Weeks 11-12)

**Goal:** Enable remaining complex libraries

**Libraries:**
1. Ark.Tools.EventSourcing (reflection for event handling)
2. Ark.Tools.RavenDb (ORM - heavy reflection)
3. Ark.Tools.Rebus (message bus - heavy reflection)
4. Remaining high-level libraries

**Expected Challenges:**
- Heavy reflection usage in ORMs and message buses
- May require DynamicallyAccessedMembers attributes
- Some libraries may not be fully trimmable

## Library Splitting Strategy

### Candidates for Splitting

**Ark.Tools.Core**
- **Reason:** Mix of simple utilities and reflection-heavy code
- **Proposal:**
  - `Ark.Tools.Core` - Trimmable utilities
  - `Ark.Tools.Core.Reflection` - Reflection extensions (not trimmable)

**Ark.Tools.Nodatime.Json**
- **Reason:** Individual converters with varying complexity
- **Proposal:** Split complex converters into separate package if needed

**Ark.Tools.Nodatime.SystemTextJson**
- **Reason:** Similar to Json variant
- **Proposal:** Same approach as Nodatime.Json

## Unblocking Strategy

### Critical Path

```
Ark.Tools.Core → [~30 libs] → Most libraries blocked
Ark.Tools.NLog → [~20 libs] → Logging widely used
Nodatime.* → [JSON chain] → Serialization blocked
```

### Priority Order

1. **Fix Ark.Tools.Core first** - Unblocks the most libraries
2. **Fix Ark.Tools.NLog second** - Enables logging for all libraries
3. **Fix Nodatime serialization chain** - Enables JSON workflows
4. **Work through dependency tree systematically**

## Patterns and Best Practices

### Established Patterns

1. **Generic Base Classes** (from Ark.Tools.Nodatime)
   - Use generics with type constraints to make types statically discoverable
   - Single suppression point with detailed justification
   - Example: `NullableNodaTimeConverter<T> where T : struct`

2. **Test Coverage**
   - Verify actual behavior (roundtrip conversions)
   - Use `TypeDescriptor.GetConverter()` for real-world scenarios
   - Test both value and null handling

3. **Suppression Justification**
   - Always include detailed justification
   - Explain why the code is trim-safe
   - Reference the specific type that makes it safe

### Common Warning Types

- **IL2026**: RequiresUnreferencedCode (most common)
- **IL2060-IL2090**: Various reflection warnings
- **IL2070**: GetInterfaces
- **IL2075-IL2076**: DI container patterns

## Metrics and Tracking

### Current Status (2026-01-10)

- **Total Libraries**: 42
- **Completed**: 3 (7%)
- **In Progress**: 0
- **Not Started**: 39 (93%)

### Target Milestones

- **Week 2**: 5 libraries (12%)
- **Week 4**: 10 libraries (24%)
- **Week 6**: 15 libraries (36%)
- **Week 10**: 30 libraries (71%)
- **Week 12**: 42 libraries (100%)

## Risk Assessment

### High Risk Items

1. **Ark.Tools.Core complexity** - May require significant refactoring
2. **Third-party library constraints** - RavenDb, Rebus may not be fully trimmable
3. **Breaking changes** - Library splitting could impact consumers

### Mitigation Strategies

1. Comprehensive testing before marking libraries as trimmable
2. Document any limitations or constraints
3. Use semantic versioning for any breaking changes
4. Provide migration guides where needed

## Resources

- [Trim Warning Codes Reference](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings)
- [Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [Introduction to trim warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings)
