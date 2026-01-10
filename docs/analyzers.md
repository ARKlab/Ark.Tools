# Added NuGet Analyzers

This document describes the additional NuGet analyzers that have been added to Ark.Tools and the rationale behind their selection.

## Research Summary

An extensive research was conducted to identify well-maintained NuGet analyzers that:
1. Are actively maintained with regular updates in 2024-2025
2. Help avoid programming mistakes or improve performance patterns
3. Have minimal overlap with existing analyzers (Microsoft.CodeAnalysis.NetAnalyzers, Meziantou.Analyzer, BannedApiAnalyzers)

## Analyzers Added

### 1. Microsoft.VisualStudio.Threading.Analyzers (v17.14.15)

**Repository:** https://github.com/microsoft/vs-threading  
**NuGet:** https://www.nuget.org/packages/Microsoft.VisualStudio.Threading.Analyzers

**Rationale:**
- **Actively Maintained:** Official Microsoft package, latest release June 2025
- **Focus:** Deep threading and async/await best practices from the Visual Studio team
- **Minimal Overlap:** While Meziantou.Analyzer has general async rules (MA0042, MA0045, MA0079, MA0080), VS Threading Analyzers provides specialized threading expertise:
  - Detects deadlocks and thread affinity issues
  - Checks for proper async/await patterns in multi-threaded environments
  - Validates TaskCompletionSource usage
  - Ensures proper handling of synchronization contexts
  
**Key Rules Enabled:**
- `VSTHRD002`: Avoid problematic synchronous waits (error)
- `VSTHRD100`: Avoid async void methods (error)
- `VSTHRD101`: Avoid unsupported async delegates (error)
- `VSTHRD110`: Observe result of async calls (error)
- `VSTHRD114`: Avoid returning null Task (error)

**Configuration:** See `.vsthreading.globalconfig`

### 2. ErrorProne.NET.CoreAnalyzers (v0.1.2)

**Repository:** https://github.com/SergeyTeplyakov/ErrorProne.NET  
**NuGet:** https://www.nuget.org/packages/ErrorProne.NET.CoreAnalyzers

**Rationale:**
- **Actively Maintained:** Regular updates, stable version used
- **Focus:** Correctness-oriented analysis for subtle bugs
  - Struct usage and readonly optimization
  - Threading and concurrency issues
  - Equality implementation problems
  - Async/await anti-patterns
- **Low Overlap:** Different focus from Meziantou (correctness vs. performance/style)

**Key Rules Enabled:**
- `EPC12`: Possible loss of information when capturing loop variables (warning)
- `EPC16`: Possible null reference when awaiting null-conditional (warning)
- `EPC17`: Avoid async lambda for void delegate (error)
- `EPC23`: Nested Task.Result calls may cause deadlock (error)
- `EPC25/EPC33`: Avoid Thread.Sleep in async methods (error)
- `EPC27`: Avoid async void methods (error)
- `EPC31`: Do not return null for Task-like types (error)
- `EPC35`: Avoid blocking calls in async methods (error)

**Configuration:** See `.errorprone.globalconfig`

## Analyzers Evaluated But Not Recommended

### Roslynator.Analyzers (v4.15.0)
- **Reason for Exclusion:** High overlap with Meziantou.Analyzer
- Both enforce similar code quality rules
- Would create many duplicate warnings
- Main value (refactorings) is better suited as IDE extension

### AsyncFixer (v2.1.0)
- **Reason for Exclusion:** Meziantou.Analyzer already provides comprehensive async rules
- Rules like MA0042, MA0045, MA0079, MA0080 cover most async anti-patterns
- Very high overlap, would be redundant

### SonarAnalyzer.CSharp (v10.18.0)
- **Reason for Exclusion:** Better suited for teams using full SonarQube ecosystem
- Significant overlap with Microsoft.CodeAnalysis.NetAnalyzers and Meziantou.Analyzer
- 470+ rules create substantial noise when combined with existing analyzers
- Best used in organizations with SonarQube/SonarCloud infrastructure

### IDisposableAnalyzers (v4.0.8)
- **Reason for Exclusion:** Maintenance concerns
- Last major update approximately 2 years ago
- Does not meet "actively maintained" requirement for 2024-2025

## Impact on Build

The new analyzers will detect issues that were previously not flagged. Some existing code may now show warnings or errors, particularly:

1. **VSTHRD200**: Methods returning Task without "Async" suffix
2. **VSTHRD110**: Unobaited async results
3. **EPC** rules: Various correctness issues

### Recommendations

1. **Review and Address Issues:** Evaluate each diagnostic and fix legitimate issues
2. **Configure Severity:** Adjust rule severity in `.vsthreading.globalconfig` and `.errorprone.globalconfig` as needed
3. **Suppress False Positives:** Use `#pragma warning disable` or `.editorconfig` for false positives
4. **Gradual Rollout:** Consider disabling strict rules initially and enabling them incrementally

## Configuration Files

- **`.vsthreading.globalconfig`**: Configuration for VSTHRD* rules
- **`.errorprone.globalconfig`**: Configuration for EPC*/ERP* rules
- **`Directory.Build.props`**: Package references and globalconfig inclusions
- **`Directory.Packages.props`**: Centralized package version management

## Overlap Analysis

### Comparison with Meziantou.Analyzer

| Category | Meziantou.Analyzer | VS Threading | ErrorProne.NET | Overlap |
|----------|-------------------|--------------|----------------|---------|
| Async naming (VSTHRD200) | ✓ (MA*) | ✓ | - | High - configured in Meziantou only |
| Async best practices | ✓ (MA0042, MA0045, etc.) | ✓ (VSTHRD100, 101, etc.) | ✓ (EPC17, 27, etc.) | Medium - complementary focus |
| Threading/deadlocks | Limited | ✓ (VSTHRD002, 110, etc.) | ✓ (EPC23, etc.) | Low - VS Threading is specialized |
| Performance (struct, LINQ) | ✓ (MA0028, MA0102, etc.) | - | Limited | Low |
| Correctness (equality, loops) | Limited | - | ✓ (EPC12, 20, 28, etc.) | Low |

### Total Rule Coverage

- **Microsoft.CodeAnalysis.NetAnalyzers**: ~300 rules (CA*, IDE*)
- **Meziantou.Analyzer**: ~200 rules (MA*)
- **Microsoft.VisualStudio.Threading.Analyzers**: ~15 rules (VSTHRD*)
- **ErrorProne.NET.CoreAnalyzers**: ~30 rules (EPC*, ERP*)

**Total**: ~545 rules with minimal duplication

## Further Reading

- [Microsoft VS Threading Analyzers Documentation](https://microsoft.github.io/vs-threading/analyzers/)
- [ErrorProne.NET GitHub](https://github.com/SergeyTeplyakov/ErrorProne.NET)
- [Meziantou.Analyzer Comparison](https://github.com/meziantou/Meziantou.Analyzer/blob/main/docs/comparison-with-other-analyzers.md)
- [.NET Code Analysis Overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview)
