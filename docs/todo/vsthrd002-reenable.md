# TODO: Re-enable VSTHRD002 Analyzer

**Created:** January 2026  
**Priority:** Medium  
**Analyzer:** VSTHRD002 - Avoid problematic synchronous waits  
**Current Status:** Disabled globally (set to `none`)

---

## Context

VSTHRD002 was disabled during the addition of Microsoft.VisualStudio.Threading.Analyzers because the codebase contains 50+ legacy synchronous wrapper methods that would require extensive refactoring. These sync-over-async patterns exist primarily for backward compatibility with legacy APIs.

**Related commit:** e635185

---

## Objectives

### 1. Obsolete CQRS Handler Sync Methods

**Target:** Application layer CQRS handlers (Commands, Queries, Requests)

**Action Items:**
- [ ] Identify all synchronous `Execute()` methods in CQRS handlers
- [ ] Mark them with `[Obsolete]` attribute with appropriate message
- [ ] Update consuming code in Application layer to use `ExecuteAsync()` methods
- [ ] Remove obsolete sync methods after migration period
- [ ] Verify no external consumers depend on sync methods

**Example Pattern:**
```csharp
[Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in future version.")]
public TResult Execute(TCommand command)
{
    return ExecuteAsync(command).GetAwaiter().GetResult();
}
```

### 2. Evaluate Processor Sync Methods

**Target:** Processors and infrastructure components

**Action Items:**
- [ ] Review all remaining sync methods to determine if they need to be retained
- [ ] For methods that must remain synchronous:
  - [ ] Evaluate using `JoinableTaskFactory` for safer synchronous waits
  - [ ] Consider using dedicated `SynchronizationContext` 
  - [ ] Document why sync method is required
  - [ ] Add targeted suppressions with justification
- [ ] Limit sync-over-async to infrastructure layer only
- [ ] Ensure all sync methods have clear documentation about threading implications

**Safe Waiting Patterns to Consider:**
```csharp
// Option 1: JoinableTaskFactory (requires Microsoft.VisualStudio.Threading package)
_joinableTaskFactory.Run(async () => await DoWorkAsync());

// Option 2: Dedicated TaskScheduler
Task.Run(() => DoWorkAsync()).GetAwaiter().GetResult();

// Option 3: Document and suppress with clear justification
#pragma warning disable VSTHRD002 // Required for legacy API compatibility - documented in [LINK]
public void LegacyMethod() 
{
    DoWorkAsync().GetAwaiter().GetResult();
}
#pragma warning restore VSTHRD002
```

### 3. Re-enable VSTHRD002 Analyzer

**Action Items:**
- [ ] After sync methods are properly obsoleted/removed/justified
- [ ] Update `.vsthreading.globalconfig` to change VSTHRD002 from `none` to `warning`
- [ ] Run full build and verify no unexpected violations
- [ ] Address any new violations that appear
- [ ] Consider promoting to `error` after stabilization period
- [ ] Update analyzer documentation

**Configuration Change:**
```editorconfig
# .vsthreading.globalconfig
# VSTHRD002: Avoid problematic synchronous waits
# Re-enabled after CQRS sync method migration
dotnet_diagnostic.VSTHRD002.severity = warning
```

---

## Migration Strategy

### Phase 1: Assessment (Week 1-2)
- Audit all synchronous wrapper methods
- Categorize by layer (Application, Infrastructure, Domain)
- Identify external dependencies on sync methods
- Create detailed migration plan

### Phase 2: Application Layer (Week 3-4)
- Obsolete CQRS handler sync methods
- Update internal consumers to use async methods
- Monitor for external usage (deprecation warnings)

### Phase 3: Infrastructure Review (Week 5-6)
- Evaluate each infrastructure sync method
- Implement safe waiting patterns where needed
- Add proper documentation and suppressions
- Consider JoinableTaskFactory integration if beneficial

### Phase 4: Re-enable Analyzer (Week 7)
- Enable VSTHRD002 as warning
- Fix any new violations
- Promote to error after confidence period
- Update team documentation

---

## Success Criteria

- [ ] All Application layer CQRS handlers use async-only methods
- [ ] Remaining sync methods limited to Infrastructure layer
- [ ] All retained sync methods have documented justification
- [ ] VSTHRD002 re-enabled at warning level minimum
- [ ] Build succeeds with 0 VSTHRD002 errors
- [ ] Zero regression in functionality
- [ ] Performance impact assessed and acceptable

---

## Dependencies

- May require coordination with consumers if public APIs are affected
- Consider API versioning strategy for breaking changes
- Evaluate if `Microsoft.VisualStudio.Threading` package should be added for JoinableTaskFactory

---

## References

- [VSTHRD002 Documentation](https://github.com/microsoft/vs-threading/blob/main/doc/analyzers/VSTHRD002.md)
- [Microsoft.VisualStudio.Threading Best Practices](https://github.com/microsoft/vs-threading/blob/main/doc/index.md)
- Original PR: #[PR_NUMBER] - Add Microsoft.VisualStudio.Threading.Analyzers
