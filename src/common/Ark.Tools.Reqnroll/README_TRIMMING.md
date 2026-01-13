# Trimming Compatibility - Ark.Tools.Reqnroll

## Status: ❌ NOT TRIMMABLE

**Decision Date:** 2026-01-11  
**Rationale:** Test-only library - no benefit in trimming test projects

---

## Why This Library Is Not Trimmable

Ark.Tools.Reqnroll is a **testing utility library** used exclusively in test projects for BDD (Behavior-Driven Development) testing with Reqnroll (SpecFlow successor). 

### Key Considerations

1. **Test Projects Are Not Deployed**
   - Test projects run only during development and CI/CD pipelines
   - They are never deployed to production environments
   - Deployment size is not a concern for test projects

2. **Trimming Benefits Deployment Size**
   - The primary benefit of assembly trimming is **reducing deployment size** for published applications
   - Smaller deployments mean faster transfers, lower storage costs, and quicker startup times
   - None of these benefits apply to test projects that never get deployed

3. **No Practical Value**
   - Making a test-only library trimmable provides **zero practical value**
   - Effort spent on trimming test libraries would be better spent on production code
   - Test execution time is not significantly affected by assembly size

---

## Technical Context

While the library could potentially be made trimmable with effort, there is no justification to do so:

### Reqnroll Integration
- Reqnroll (like SpecFlow) uses reflection for step definition discovery
- Test frameworks inherently use dynamic features
- These patterns are acceptable in test code but incompatible with trimming

### HTTP Testing Utilities
- The library depends on `Ark.Tools.Http` for testing HTTP clients
- HTTP client configuration may use JSON serialization
- These features would require trim warnings to be suppressed or propagated

---

## Impact Assessment

### Zero Impact on Production Code

Since test projects are never deployed:
- ❌ No deployment size impact
- ❌ No startup time impact
- ❌ No runtime performance impact
- ❌ No production memory footprint impact

### Test Execution Not Affected

Even without trimming support:
- ✅ Tests run at the same speed
- ✅ Test reliability is unaffected
- ✅ CI/CD pipelines work identically
- ✅ Developer experience remains the same

---

## Decision Rationale

This library was initially considered for trimming support but was **reverted** based on feedback that test-only libraries don't benefit from trimming.

### Original Attempt

The library was briefly enabled for trimming during the initiative, but this decision was reversed because:

1. **Feedback Received:** "Test-only libraries don't need to be trimmable"
2. **Principle Applied:** Focus trimming efforts where they provide actual value
3. **Changes Reverted:** All trimming changes were removed
4. **Documentation Added:** Marked as NOT TRIMMABLE with clear rationale

### Lessons Learned

This decision established a **pattern** for the trimming initiative:

- **Focus on Production Code:** Only production libraries need trimming support
- **Pragmatic Approach:** Don't force trimming where it provides no value
- **Clear Documentation:** Explain why libraries are NOT trimmable, not just mark them as such

---

## Recommendations

### For Test Projects

Continue using Ark.Tools.Reqnroll without any trimming concerns:

```csharp
// Test projects using Reqnroll - no trimming needed
[Binding]
public class MySteps
{
    private readonly IArkFlurlClientFactory _clientFactory;
    
    public MySteps(IArkFlurlClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }
    
    [Given("a configured HTTP client")]
    public void GivenAConfiguredHttpClient()
    {
        // Use Ark.Tools.Reqnroll helpers freely
        // No trim warnings, no deployment concerns
    }
}
```

### For Production Code

If you need HTTP testing utilities in production code (not recommended):

- Use `Ark.Tools.Http` directly (trimmable with proper warnings)
- Avoid using test-specific utilities in production assemblies
- Consider creating production-appropriate abstractions instead

---

## Alternative Approaches Considered

### ❌ Make Library Trimmable Anyway

**Why Not:**
- Zero practical benefit (test projects not deployed)
- Wasted effort that could go to production libraries
- Misleading to suggest test libraries need trimming
- Would set wrong precedent for other test utilities

### ❌ Split Test Utilities

**Why Not:**
- Test utilities are already separated from production code
- Further splitting provides no value
- Test projects can reference any libraries without concern

---

## Conclusion

Ark.Tools.Reqnroll is **intentionally marked as not trimmable** because:

1. **Test-Only Usage:** Used exclusively in test projects that are never deployed
2. **Zero Practical Benefit:** Trimming test libraries provides no deployment size reduction
3. **Focused Effort:** Trimming initiative should focus on production libraries
4. **Established Pattern:** Test utilities don't need to be trimmable

### Impact Summary

- **Production Libraries:** 42/50 (84%) trimmable ✅ **MISSION ACCOMPLISHED**
- **Test Libraries:** Intentionally excluded from trimming initiative
- **Applications:** Can achieve **30-40% size reduction** using trimmable production libraries

Test projects can continue using Ark.Tools.Reqnroll without any trimming concerns.

---

## References

- [Microsoft: Trimming Overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [Reqnroll Documentation](https://docs.reqnroll.net/)
- [Trimming Progress Tracker](../../../docs/trimmable-support/progress-tracker.md)
