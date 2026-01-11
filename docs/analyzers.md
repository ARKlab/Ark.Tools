# Code Analyzers in Ark.Tools

This document describes the NuGet analyzer packages used in Ark.Tools and the rationale for their inclusion and configuration.

## Overview

Ark.Tools uses multiple analyzer packages to maintain code quality, prevent bugs, and enforce best practices. The analyzers are configured to work together with minimal overlap while providing comprehensive coverage of common issues.

## Installed Analyzers

### 1. Microsoft.CodeAnalysis.NetAnalyzers (Built-in)

**Source:** Built into .NET SDK  
**Rules:** ~300 rules (CA*, IDE*)  
**Purpose:** Core .NET code analysis including security, performance, design guidelines

This is the foundation analyzer that comes with the .NET SDK. It enforces Microsoft's Framework Design Guidelines and detects common security and performance issues.

**Configuration:** Configured via `.editorconfig` and `.globalconfig` files

### 2. Meziantou.Analyzer

**NuGet:** https://www.nuget.org/packages/Meziantou.Analyzer  
**Repository:** https://github.com/meziantou/Meziantou.Analyzer  
**Rules:** ~200 rules (MA*)  
**Purpose:** General code quality, performance optimizations, and best practices

Meziantou.Analyzer is the primary third-party analyzer providing comprehensive coverage of:
- Performance patterns (LINQ optimization, struct usage, string operations)
- Async/await best practices
- Code style and maintainability
- API usage correctness

**Key Features:**
- Actively maintained with frequent updates
- Comprehensive rule comparison documentation
- Low false positive rate

**Configuration:** `.meziantou.globalconfig`

### 3. Microsoft.VisualStudio.Threading.Analyzers

**NuGet:** https://www.nuget.org/packages/Microsoft.VisualStudio.Threading.Analyzers  
**Repository:** https://github.com/microsoft/vs-threading  
**Version:** 17.14.15  
**Rules:** ~15 rules (VSTHRD*)  
**Purpose:** Specialized threading and async/await pattern analysis

**Why Added:**
- Official Microsoft package with deep threading expertise from the Visual Studio team
- Detects deadlock scenarios and thread affinity issues not covered by general analyzers
- Validates complex async patterns (TaskCompletionSource, synchronization contexts)
- Complementary to Meziantou's general async rules with specialized threading focus

**Key Rules:**
- `VSTHRD003` (error): Avoid awaiting foreign Tasks that could cause deadlocks
- `VSTHRD100` (error): Avoid async void methods
- `VSTHRD110` (error): Observe result of async calls
- `VSTHRD114` (error): Avoid returning null Task

**Disabled Rules:**
- `VSTHRD002` (none): Avoid synchronous waits - disabled due to legacy sync wrappers (see `docs/todo/vsthrd002-reenable.md` for migration plan)
- `VSTHRD111` (none): Use ConfigureAwait - disabled in favor of MA0004
- `VSTHRD200` (none): Use "Async" suffix - disabled per team preference

**Configuration:** `.vsthreading.globalconfig`

**Documentation:** https://microsoft.github.io/vs-threading/analyzers/

### 4. ErrorProne.NET.CoreAnalyzers

**NuGet:** https://www.nuget.org/packages/ErrorProne.NET.CoreAnalyzers  
**Repository:** https://github.com/SergeyTeplyakov/ErrorProne.NET  
**Version:** 0.1.2  
**Rules:** ~30 rules (EPC*, ERP*)  
**Purpose:** Correctness-focused analysis for subtle bugs

**Why Added:**
- Focus on correctness issues (struct usage, equality, threading, async anti-patterns)
- Catches subtle bugs not detected by other analyzers
- Low overlap with existing analyzers
- Actively maintained

**Key Rules:**
- `EPC12` (warning): Possible loss of information when capturing loop variables
- `EPC17` (error): Avoid async lambda for void delegate
- `EPC23` (error): Nested Task.Result calls may cause deadlock
- `ERP022` (error): Exit point swallows an unobserved exception
- `ERP023` (error): Possible multiple enumeration of IEnumerable
- `EPC31` (error): Do not return null for Task-like types
- `EPC35` (error): Avoid blocking calls in async methods

**Disabled Rules:**
- `EPC15` (none): Use ConfigureAwait(false) - disabled in favor of MA0004

**Configuration:** `.errorprone.globalconfig`

**Documentation:** https://github.com/SergeyTeplyakov/ErrorProne.NET

### 5. Microsoft.CodeAnalysis.BannedApiAnalyzers

**NuGet:** https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers  
**Purpose:** Prevent usage of specific APIs via BannedSymbols.txt

This analyzer allows the team to ban specific APIs that should not be used in the codebase.

**Configuration:** `BannedSymbols.txt` files in projects

## Configuration Strategy

### ConfigureAwait Rule Consolidation

Three analyzers provide ConfigureAwait warnings. To avoid requiring multiple suppressions for the same issue, only **MA0004** is enabled:

- `MA0004` (Meziantou) - **WARNING** ✅ Primary rule
- `VSTHRD111` (VS Threading) - **NONE** (disabled)
- `EPC15` (ErrorProne.NET) - **NONE** (disabled)

**Rationale:** Developers only need to address one warning per missing ConfigureAwait instead of three duplicate warnings.

### Severity Levels

Analyzers use different severity levels based on the impact of violations:

- **Error:** Build fails - used for critical issues (security, correctness, deadlocks)
- **Warning:** Build succeeds but shows warnings - used for important best practices
- **Suggestion:** IDE hint only - used for style preferences
- **None:** Rule disabled - used for rules with high false positives or not applicable

### Configuration Files

- **Root Level:**
  - `.meziantou.globalconfig` - Meziantou.Analyzer rules
  - `.vsthreading.globalconfig` - VS Threading Analyzer rules
  - `.errorprone.globalconfig` - ErrorProne.NET rules
  - `.editorconfig` - General editor and IDE* rules

- **Sample Projects:** Inherit root configuration, demonstrating production-quality patterns

- **Test Projects:** 
  - `.meziantou.globalconfig` - MA0004 downgraded to suggestion (tests can be more pragmatic)
  - Additional test-specific overrides as needed

## Overlap Analysis

| Category | .NET Analyzers | Meziantou | VS Threading | ErrorProne.NET | Primary Analyzer |
|----------|---------------|-----------|--------------|----------------|------------------|
| Security | ✓ Comprehensive | Limited | - | - | .NET Analyzers |
| Performance | ✓ Basic | ✓ Comprehensive | - | Limited | Meziantou |
| Async naming | ✓ Basic | ✓ | ✓ (disabled) | - | Meziantou |
| General async patterns | ✓ Basic | ✓ Comprehensive | ✓ Complementary | ✓ Anti-patterns | Meziantou + VS Threading |
| Threading/deadlocks | Limited | Limited | ✓ **Specialized** | ✓ Limited | VS Threading |
| Correctness (equality, structs) | ✓ Basic | Limited | - | ✓ **Core focus** | ErrorProne.NET |
| ConfigureAwait | ✓ | ✓ **Primary** | ✓ (disabled) | ✓ (disabled) | Meziantou (MA0004) |

**Total Coverage:** ~545 rules with minimal duplication

**Result:** Each analyzer brings unique value with complementary focus areas. Overlapping rules are strategically disabled to avoid duplicate warnings.

## Analyzers Considered But Not Included

### Roslynator.Analyzers
**Reason for Exclusion:** High overlap with Meziantou.Analyzer (200+ duplicate rules), would create excessive noise

### AsyncFixer
**Reason for Exclusion:** Redundant with Meziantou.Analyzer's comprehensive async rules (MA0042, MA0045, MA0079, MA0080)

### SonarAnalyzer.CSharp
**Reason for Exclusion:** Better suited for organizations using full SonarQube ecosystem, significant overlap (470 rules), would create excessive noise when combined with existing analyzers

### IDisposableAnalyzers
**Reason for Exclusion:** No longer actively maintained (last major update ~2 years ago)

## Migration and Future Work

### VSTHRD002 Re-enablement Plan

VSTHRD002 (Avoid synchronous waits) is currently disabled due to extensive legacy sync-over-async patterns in the codebase (50+ locations). A comprehensive migration plan exists to eventually re-enable this rule:

**See:** `docs/todo/vsthrd002-reenable.md` for detailed 4-phase migration strategy including:
- Phase 1: Obsolete sync methods in CQRS handler interfaces (Application layer)
- Phase 2: Evaluate sync methods in processor interfaces (Infrastructure layer)
- Phase 3: Implement safe waiting patterns where sync methods must be retained
- Phase 4: Re-enable VSTHRD002 analyzer

## Maintaining This Configuration

### Adding New Rules

When considering new analyzer rules or packages:

1. **Check for overlap:** Review existing analyzer coverage using comparison documentation
2. **Evaluate maintenance:** Ensure active development with recent updates
3. **Assess false positives:** Test on a subset of code to gauge noise level
4. **Document decision:** Update this file with rationale

### Adjusting Severity

To change rule severity:

1. Edit appropriate `.globalconfig` file (`.meziantou.globalconfig`, `.vsthreading.globalconfig`, or `.errorprone.globalconfig`)
2. Use standard EditorConfig syntax: `dotnet_diagnostic.<RULE_ID>.severity = <error|warning|suggestion|none>`
3. Document reason in comments if disabling default behavior
4. Consider impact on CI/CD builds (TreatWarningsAsErrors=true)

### Suppressing Individual Violations

For legitimate cases where a rule doesn't apply:

```csharp
#pragma warning disable RULE_ID // Reason why suppression is needed
// Code that violates the rule
#pragma warning restore RULE_ID
```

Always include a comment explaining why the suppression is necessary.

## References

- [Meziantou.Analyzer Documentation](https://github.com/meziantou/Meziantou.Analyzer)
- [Meziantou.Analyzer Comparison with Other Analyzers](https://github.com/meziantou/Meziantou.Analyzer/blob/main/docs/comparison-with-other-analyzers.md)
- [Microsoft VS Threading Analyzers](https://microsoft.github.io/vs-threading/analyzers/)
- [ErrorProne.NET GitHub](https://github.com/SergeyTeplyakov/ErrorProne.NET)
- [.NET Code Analysis Overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview)
- [EditorConfig for .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
