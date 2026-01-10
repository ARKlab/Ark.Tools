# Next Steps After Adding New Analyzers

## What Was Added

Two new NuGet analyzers have been added to the Ark.Tools repository:

1. **Microsoft.VisualStudio.Threading.Analyzers** v17.14.15
2. **ErrorProne.NET.CoreAnalyzers** v0.1.2

See `docs/analyzers.md` for complete details on the selection rationale and overlap analysis.

## Current Build Status

The new analyzers are working correctly and have identified issues in the existing codebase. When building projects, you will now see new diagnostics, particularly:

### Common Issues Found

1. **VSTHRD200**: Methods returning `Task` or `Task<T>` without "Async" suffix
   - Example: `public Task RunForEach(...)` should be `public Task RunForEachAsync(...)`
   
2. **VSTHRD110**: Unobserved awaitable results
   - Example: Fire-and-forget calls like `task.ConfigureAwait(false);` without await/assignment
   
3. **Various EPC rules**: Correctness issues in struct usage, equality, async patterns

## Recommended Actions

### Option 1: Address Issues (Recommended)

Review each diagnostic and fix legitimate issues. Many of these represent real bugs or maintainability problems:

```csharp
// Before (VSTHRD200 violation)
public Task RunForEach(...) { }

// After
public Task RunForEachAsync(...) { }
```

### Option 2: Adjust Severity Levels

If certain rules are too strict for your codebase, adjust their severity in the globalconfig files:

**`.vsthreading.globalconfig`**:
```
# Change VSTHRD200 from error to warning
dotnet_diagnostic.VSTHRD200.severity = warning

# Or disable it completely
dotnet_diagnostic.VSTHRD200.severity = none
```

**`.errorprone.globalconfig`**:
```
# Change EPC rules as needed
dotnet_diagnostic.EPC12.severity = suggestion
```

### Option 3: Suppress Specific Instances

For false positives or cases where the diagnostic doesn't apply:

```csharp
#pragma warning disable VSTHRD200
public Task RunForEach(...) // Has valid reason not to use Async suffix
{
    // ...
}
#pragma warning restore VSTHRD200
```

### Option 4: Gradual Rollout

1. Start with all new rules as `suggestion` or `warning`
2. Fix high-priority issues first (errors)
3. Gradually increase severity as code quality improves

Edit the globalconfig files to set initial severity:
```
# Start lenient
dotnet_diagnostic.VSTHRD200.severity = suggestion
dotnet_diagnostic.VSTHRD110.severity = warning
```

## Configuration Files

- **`.vsthreading.globalconfig`**: Configure VSTHRD* rules
- **`.errorprone.globalconfig`**: Configure EPC*/ERP* rules
- **`docs/analyzers.md`**: Complete documentation

## Testing the Changes

To see what the analyzers found:

```bash
# Build a single project
dotnet build src/common/Ark.Tools.Core/Ark.Tools.Core.csproj --no-restore

# Build entire solution
dotnet build --no-restore

# See all diagnostics
dotnet build --no-restore /p:TreatWarningsAsErrors=false
```

## Benefits

Once issues are addressed, the codebase will have:

✅ Fewer threading bugs and deadlocks (VS Threading Analyzers)  
✅ Better async/await patterns (both analyzers)  
✅ Fewer subtle correctness issues (ErrorProne.NET)  
✅ More maintainable code with consistent naming (VSTHRD200)  
✅ ~45 additional rules checking for common mistakes

## Need Help?

- Review `docs/analyzers.md` for complete documentation
- Check analyzer documentation:
  - [VS Threading Analyzers](https://microsoft.github.io/vs-threading/analyzers/)
  - [ErrorProne.NET](https://github.com/SergeyTeplyakov/ErrorProne.NET)
- Adjust severity levels in globalconfig files
- Use `#pragma warning disable` for false positives
