# Trimming Implementation Plan

**Last Updated:** 2026-01-11  
**Status:** Phase 2 - In Progress

## Executive Summary

This document outlines the strategy for making Ark.Tools libraries trim-compatible. The work is divided into phases based on dependency order, with the goal of making **as many libraries trimmable as feasible**.

## Goals and Philosophy

### Primary Goals

1. **Make as many libraries trimmable as feasible** - Not every library needs to be trim-compatible
2. **Reduce deployment sizes** by 30-40% for trimmed applications using Ark.Tools
3. **Maintain backward compatibility** with existing code
4. **Establish patterns** for trim-safe code in future development
5. **Document clearly** which libraries are not trimmable and why

### When NOT to Force Trimming

It is **perfectly acceptable** to leave a library as non-trimmable if:

- The library fundamentally requires dynamic reflection that cannot be statically analyzed
- Making it trim-safe would require breaking changes or major refactoring
- The complexity/effort outweighs the benefits for that specific library
- The library is rarely used in trim-sensitive deployment scenarios (e.g., development tools, build-time utilities)
- The library depends on third-party packages that are fundamentally not trim-compatible

**Document the reason** in the library's README or in this plan when deciding not to pursue trimming support.

## Implementation Strategy

### Phase-Based Approach

The implementation follows a dependency-ordered approach, starting with libraries that have no Ark.Tools dependencies and working up the dependency tree.

### Success Criteria

For each library to be marked as trimmable:
- ✅ Build succeeds with `<IsTrimmable>true</IsTrimmable>`
- ✅ Build succeeds with `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`
- ✅ Zero IL#### trim warnings (or all warnings properly suppressed with valid justifications)
- ✅ All existing tests pass
- ✅ Roundtrip/integration tests added for trim-sensitive code

### Success Criteria for Non-Trimmable Decision

For libraries intentionally left as non-trimmable:
- ✅ Clear documentation of **why** the library is not trimmable
- ✅ Assessment that the cost of making it trimmable outweighs the benefit
- ✅ Consideration of whether the library could be split (trim-safe core + reflection-heavy extensions)
- ✅ Verification that dependent libraries can still be made trimmable

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
5. ⏳ **Ark.Tools.Auth0** - Has trim warnings (IL2026 from dynamic types)
6. ⏳ **Ark.Tools.Hosting** - Has trim warnings (IL2026 from ConfigurationBinder)
7. ⏳ **Ark.Tools.SimpleInjector** - Has trim warnings (IL2076 from Lazy<T>)
8. ⏳ **Ark.Tools.Core** - Deferred (high complexity - 9 warning types)

**Deliverables:**
- [x] Pattern for generic base classes
- [x] Test project template
- [ ] Fix trim warnings in Level 0 libraries (Auth0, Hosting, SimpleInjector)
- [ ] Documentation on handling IL2026 warnings
- [ ] Core library trim analysis (deferred to later phase)

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

### Current Status (2026-01-11)

- **Total Libraries**: 42
- **Completed**: 15 (36%)
- **In Progress**: 0
- **Not Started**: 27 (64%)

### Target Milestones

- **Week 2**: 5 libraries (12%) ✅ Exceeded - 15 completed
- **Week 4**: 10 libraries (24%)
- **Week 6**: 15 libraries (36%)
- **Week 10**: 30 libraries (71%)
- **Week 12**: 42 libraries (100%)

## TODO Items

### Testing and Validation

- [ ] **Add test case for Dictionary with convertible keys** (Priority: High)
  - Location: `samples/Ark.ReferenceProject/WebApplicationDemo` or new test project
  - Test DTOs with custom types as dictionary keys (e.g., `ProductId`, `OrderNumber`)
  - Test DTOs with `Dictionary<OffsetDateTime, TValue>` and other NodaTime types as keys
  - Verify serialization/deserialization works correctly with .NET 9+ (requires `TypeDescriptor.RegisterType`)
  - Verify .NET 8 continues to work without registration
  - Document findings in `docs/trimmable-support/progress-tracker.md`

- [ ] **Add test case for polymorphism with System.Text.Json** (Priority: Medium)
  - Location: `samples/Ark.ReferenceProject/WebApplicationDemo`
  - Test polymorphic serialization using `JsonPolymorphicConverter`
  - Verify trimming compatibility
  - Document any registration requirements

### Research Items

- [x] **NullableStructSerializer.cs: Check if still needed in .NET 8+**
  - **Status**: KEEP - Still provides value
  - **Reason**: While .NET 8 improved nullable struct support, this converter provides explicit handling for nullable structs with custom converters, ensuring consistent behavior
  - **Context**: Added for STJ v6 to support generic nullable struct for structs with custom converters
  - **Decision**: Keep for now, but can be removed in future if .NET adds native support

- [x] **UniversalInvariantTypeConverterJsonConverter.cs: Check if issue #38812 resolved in .NET 8**
  - **Status**: KEEP - Issue NOT resolved
  - **Issue**: [GitHub #38812](https://github.com/dotnet/runtime/issues/38812) - System.Text.Json does not support TypeConverters
  - **.NET 8 Status**: Still NOT supported, issue closed as "Won't Fix" - System.Text.Json will not support TypeConverter attributes by design
  - **Reason**: This converter bridges the gap for types decorated with `TypeConverterAttribute`, which is still needed
  - **Decision**: Keep indefinitely, this is a permanent gap in System.Text.Json vs Newtonsoft.Json

### Documentation

- [x] Add migration guide for TypeConverter registration in v6 migration doc
- [x] Update implementation plan with TODO items
- [x] Clarify that GetConverterFromRegisteredType applies to ALL .NET 9+ apps, not just trimmed apps

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
