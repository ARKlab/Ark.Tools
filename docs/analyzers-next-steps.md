# Analyzer Configuration Reference

This document provides practical guidance for working with the analyzers in Ark.Tools.

## Quick Reference

### Configuration Files

- **`.meziantou.globalconfig`** - Meziantou.Analyzer rules (MA*)
- **`.vsthreading.globalconfig`** - VS Threading Analyzer rules (VSTHRD*)
- **`.errorprone.globalconfig`** - ErrorProne.NET rules (EPC*, ERP*)
- **`.editorconfig`** - General editor and IDE rules

### Current Severity Levels

**Error (Build-breaking):**
- `VSTHRD003` - Avoid awaiting foreign Tasks
- `VSTHRD100` - Avoid async void methods
- `ERP023` - Possible multiple enumeration of IEnumerable
- `ERP022` - Exit point swallows an unobserved exception
- Plus standard error-level rules from .NET Analyzers

**Warning:**
- `MA0004` - Use Task.ConfigureAwait (primary ConfigureAwait rule)
- Plus standard warning-level rules from .NET Analyzers and Meziantou

**Suggestion:**
- Various code quality and style rules (see globalconfig files)
- `MA0004` in test projects (more pragmatic for tests)

**Disabled:**
- `VSTHRD002` - Avoid synchronous waits (migration plan in `docs/todo/vsthrd002-reenable.md`)
- `VSTHRD111` - Use ConfigureAwait (disabled in favor of MA0004)
- `VSTHRD200` - Use "Async" suffix (team preference)
- `EPC15` - Use ConfigureAwait(false) (disabled in favor of MA0004)

## Common Scenarios

### Fixing ConfigureAwait Warnings (MA0004)

```csharp
// ❌ Warning: MA0004
await SomeMethodAsync();

// ✅ Fixed - Library code
await SomeMethodAsync().ConfigureAwait(false);

// ✅ Fixed - Application code (UI/ASP.NET Core)
await SomeMethodAsync().ConfigureAwait(true);
// Or just:
await SomeMethodAsync(); // ASP.NET Core has no SynchronizationContext

// ✅ Fixed - await using pattern
await using var resource = await CreateAsync().ConfigureAwait(false);

// ✅ Concise pattern for context disposal
await using var _ = ctx.ConfigureAwait(false);
```

### Handling Multiple Enumeration (ERP023)

```csharp
// ❌ Error: ERP023
IEnumerable<int> numbers = GetNumbers();
int count = numbers.Count();
int sum = numbers.Sum(); // Multiple enumeration

// ✅ Fixed - Materialize once
IReadOnlyList<int> numbers = GetNumbers().ToList();
int count = numbers.Count;
int sum = numbers.Sum();

// ✅ Or pass to single LINQ chain
int sum = GetNumbers().Sum();
```

### Avoiding Async Void (VSTHRD100)

```csharp
// ❌ Error: VSTHRD100
public async void ProcessData()
{
    await DoWorkAsync();
}

// ✅ Fixed - Return Task
public async Task ProcessDataAsync()
{
    await DoWorkAsync();
}

// ✅ For event handlers (only valid async void use case)
private async void Button_Click(object sender, EventArgs e)
{
    try
    {
        await ProcessDataAsync();
    }
    catch (Exception ex)
    {
        // Handle exception - critical for async void
        _logger.LogError(ex, "Error processing data");
    }
}
```

### Observing Async Results (VSTHRD110)

```csharp
// ❌ Error: VSTHRD110
task.ContinueWith(...); // Result not observed

// ✅ Fixed - Explicit fire-and-forget
_ = task.ContinueWith(...);

// ✅ Better - Await the result
await task.ContinueWith(...);
```

### Empty Catch Blocks (ERP022)

```csharp
// ❌ Error: ERP022
try
{
    await AuthenticateAsync();
}
catch
{
    // Swallowed exception
}

// ✅ Fixed - Log at appropriate level
try
{
    await AuthenticateAsync();
}
catch (Exception ex)
{
    _logger.LogTrace(ex, "Authentication failed"); // Trace for expected failures
}

// ✅ Or suppress with justification
#pragma warning disable ERP022 // Authentication failures are expected and safely ignored
try
{
    await AuthenticateAsync();
}
catch
{
}
#pragma warning restore ERP022
```

## Adjusting Severity Levels

### In GlobalConfig Files

Edit the appropriate `.globalconfig` file:

```ini
# Make a rule more strict
dotnet_diagnostic.MA0051.severity = warning  # Method too long

# Make a rule less strict
dotnet_diagnostic.VSTHRD011.severity = suggestion  # Use AsyncLazy

# Disable a rule
dotnet_diagnostic.MA0015.severity = none  # Specify parameter name
```

### In .editorconfig (File/Directory Specific)

Create or edit `.editorconfig` in specific directory:

```ini
# tests/.editorconfig
[*.cs]
# Tests can be more pragmatic about ConfigureAwait
dotnet_diagnostic.MA0004.severity = suggestion
```

### Per-File or Per-Method

```csharp
#pragma warning disable RULE_ID // Always include reason
// Code that violates rule
#pragma warning restore RULE_ID
```

## Understanding Rule IDs

- **CA***: .NET Code Analysis rules
- **IDE***: IDE code style rules
- **MA***: Meziantou.Analyzer rules
- **VSTHRD***: Visual Studio Threading Analyzer rules
- **EPC***: ErrorProne.NET Compiler rules
- **ERP***: ErrorProne.NET Runtime rules

## Sample Projects

Sample projects (`samples/Ark.ReferenceProject/`, `samples/Ark.ResourceWatcher/`) inherit the same analyzer configuration as main source code. They demonstrate production-quality patterns and best practices.

**Philosophy:** Samples are production-quality demonstration code, not tutorials that should suppress warnings.

## Test Projects

Test projects have pragmatic analyzer overrides:

- `MA0004` (ConfigureAwait) - Downgraded to suggestion
- Tests don't need the same rigor as library code for synchronization context

**Location:** `tests/.meziantou.globalconfig` and project-specific overrides

## Troubleshooting

### Build Fails with New Analyzer Warnings

If build suddenly fails after updating analyzers or adding new code:

1. **Review the diagnostic:** Is it a legitimate issue?
   - Yes → Fix the code
   - No → Consider suppression or severity adjustment

2. **Temporary workaround for local builds:**
   ```bash
   dotnet build /p:TreatWarningsAsErrors=false
   ```

3. **Long-term solution:**
   - Fix the issues (preferred)
   - Adjust severity in globalconfig if rule is too strict
   - Add targeted suppressions for false positives

### Too Many Warnings

If a rule generates excessive noise:

1. Review several instances - are they legitimate issues?
2. If mostly false positives: Disable rule (`severity = none`)
3. If mostly real issues: 
   - Fix incrementally
   - Or downgrade to suggestion temporarily
   - Plan gradual remediation

### Conflicting Rules

If two rules conflict:

1. Check overlap analysis in `docs/analyzers.md`
2. Disable the less specific rule
3. Document the decision in globalconfig comments

## CI/CD Considerations

The build uses `TreatWarningsAsErrors=true`, so:

- **Warnings = Build failures** in CI
- Severity levels have direct impact on build success
- Test locally before pushing: `dotnet build`

## Getting More Information

### About Specific Rules

- **Meziantou:** https://github.com/meziantou/Meziantou.Analyzer/tree/main/docs/Rules
- **VS Threading:** https://microsoft.github.io/vs-threading/analyzers/
- **ErrorProne.NET:** https://github.com/SergeyTeplyakov/ErrorProne.NET
- **.NET CA rules:** https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/

### In Visual Studio

Hover over the squiggle or press `Ctrl+.` to see:
- Rule description
- Quick fixes
- Suppress options

### From Command Line

```bash
# See all diagnostics
dotnet build --no-restore

# See diagnostics for specific project
dotnet build src/common/Ark.Tools.Core/Ark.Tools.Core.csproj
```

## Future Migration: VSTHRD002

`VSTHRD002` (Avoid synchronous waits) is currently disabled due to legacy sync-over-async patterns in CQRS handler interfaces.

**See:** `docs/todo/vsthrd002-reenable.md` for the comprehensive 4-phase migration plan to eventually re-enable this important rule.

## Contributing

When making analyzer configuration changes:

1. Update appropriate globalconfig file
2. Test the change locally
3. Document rationale in PR description
4. Update this file or `docs/analyzers.md` if adding/removing analyzers
