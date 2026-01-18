# Trimming Support Documentation

This folder contains documentation for the assembly trimming initiative across all Ark.Tools libraries.

## Overview

Assembly trimming is a .NET feature that removes unused code from published applications, significantly reducing deployment size. This initiative aims to make all Ark.Tools libraries trim-compatible to enable optimal deployment sizes for applications using these libraries.

## Documentation Files

- **[overhaul-plan.md](overhaul-plan.md)** - 🆕 **CRITICAL** - Comprehensive plan to achieve 100% Trimmable libraries
- **[guidelines.md](guidelines.md)** - ⭐ Best practices and patterns for making libraries trimmable
- **[implementation-plan.md](implementation-plan.md)** - Original strategy and completion report (being revised)
- **[progress-tracker.md](progress-tracker.md)** - Detailed status for all 50 libraries (being updated)
- **[deep-dive-analysis.md](deep-dive-analysis.md)** - Technical analysis of complexity and effort

## Quick Status

- **Status**: 🔄 **OVERHAUL IN PROGRESS** (2026-01-18)
- **Current Progress**: **42/50 libraries (84%) trimmable**
  - Common Libraries: 37/43 (86%) ✅
  - ResourceWatcher Libraries: 7/8 (88%) ✅
  - AspNetCore Libraries: 0/11 (0%) - Under review
- **NEW GOAL**: **100% of src/ libraries Trimmable** with RequiresUnreferencedCode where needed
- **Key Insight**: A library CAN be Trimmable even with RequiresUnreferencedCode methods

## Recent Updates (2026-01-18)

### ⚠️ IMPORTANT: Direction Change After Review

Based on feedback from @AndreaCuneo, the original approach needs significant revision:

**Previous Understanding (INCORRECT):**
- Some libraries should remain "not trimmable"
- Split Core.Reflection into separate package
- Accept 84% as final result

**Corrected Understanding (CORRECT):**
- **A library is Trimmable as long as all warnings are handled**, including exposing RequiresUnreferencedCode
- Libraries CAN have methods with RequiresUnreferencedCode and still be marked `<IsTrimmable>true</IsTrimmable>`
- **ALL libraries under src/ MUST be Trimmable** (100% goal)
- Core.Reflection should be merged back with RequiresUnreferencedCode on methods

### New Overhaul Plan

See **[overhaul-plan.md](overhaul-plan.md)** for comprehensive strategy:

**Phase 1:** Review UnconditionalSuppressMessage usage - replace inappropriate uses with RequiresUnreferencedCode

**Phase 2:** Merge Core.Reflection back into Core with RequiresUnreferencedCode attributes

**Phase 3:** Make all 6 remaining common libraries Trimmable with RequiresUnreferencedCode

**Phase 4:** Investigate AspNetCore libraries for Trimmable status

**Expected Outcome:** 100% of src/ libraries Trimmable

## For Library Authors

**Best Practices:**
1. Read [guidelines.md](guidelines.md) for patterns and anti-patterns
2. Use `RequiresUnreferencedCode` to propagate warnings to public APIs
3. Use `DynamicallyAccessedMembers` when types are known at compile time
4. Use `UnconditionalSuppressMessage` only when genuinely safe (with detailed justification)
5. Prefer avoiding reflection when possible
6. Consider source generators for reflection-heavy scenarios

**When NOT to Make a Library Trimmable:**
- Reflection is fundamental to the library's purpose
- Third-party dependencies are not trim-compatible
- Making it trim-safe requires breaking changes
- It's a test/dev-only library
- Benefits don't justify the complexity

## For Application Developers

**Using Trimmable Libraries:**
```xml
<!-- Enable trimming in your application -->
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
</PropertyGroup>
```

**Migration from v5 to v6:**
- Most applications: No changes needed ✅
- Using reflection features: Add `Ark.Tools.Core.Reflection` package
- See [migration-v6.md](../migration-v6.md#arktools-corereflection-split-trimming-support)

## References

### Microsoft Official Documentation

- [Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [Understanding trim analysis](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-concepts)
- [Fixing trim warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings)
- [Trim warnings reference](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings)
- [RequiresUnreferencedCodeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.requiresunreferencedcodeattribute)
- [DynamicallyAccessedMembersAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute)
- [UnconditionalSuppressMessageAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.unconditionalsuppressmessageattribute)

### Ark.Tools Documentation

- [Migration Guide v6](../migration-v6.md)
- [Core.Reflection README](../../src/common/Ark.Tools.Core.Reflection/README_TRIMMING.md)

---

**Last Updated:** 2026-01-18  
**Status:** ✅ COMPLETE WITH VALIDATION  
**Next Review:** When considering v7 release (optional enhancements)
