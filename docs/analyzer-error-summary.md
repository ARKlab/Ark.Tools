# Analyzer Errors and Configuration Summary

**Last Updated:** After adding analyzers to sample projects (Ark.ReferenceProject and Ark.ResourceWatcher)

**Scope:** All errors listed below apply to:
- Main source code (`src/`)
- Sample projects (`samples/Ark.ReferenceProject/`, `samples/Ark.ResourceWatcher/`)

## Active Errors (TreatWarningsAsErrors=true)

### VSTHRD110: Observe the awaitable result of this method call
**Count:** 2 unique locations (4 total occurrences due to multi-targeting)

**Location 1:** `src/common/Ark.Tools.Core/TaskExtensions.cs` line 12
```csharp
task.ContinueWith(c => { var ignored = c.Exception; },
```
**Context:** In `IgnoreExceptions()` method - intentionally fire-and-forget pattern for exception handling
**Suggested Action:** Suppress with `#pragma warning disable VSTHRD110` or assign to `_ = task.ContinueWith(...)`

**Location 2:** `src/common/Ark.Tools.Core/TaskExtensions.cs` line 22
```csharp
task.ContinueWith(c => Environment.FailFast("Task faulted", c.Exception),
```
**Context:** In `FailFastOnException()` method - intentionally fire-and-forget pattern for FailFast
**Suggested Action:** Suppress with `#pragma warning disable VSTHRD110` or assign to `_ = task.ContinueWith(...)`

---

### VSTHRD002: Synchronously waiting on tasks may cause deadlocks
**Count:** 1 occurrence

**Location:** `src/aspnetcore/Ark.Tools.AspNetCore.NestedStartup/Ex.cs` line 32
```csharp
=> webHostBuilder.StopAsync().GetAwaiter().GetResult()
```
**Context:** In callback registered with `IHostApplicationLifetime.ApplicationStopping` - synchronous context
**Suggested Action:** 
- Option 1: Suppress (callback must be synchronous)
- Option 2: Use `JoinableTaskFactory.Run()` if available
- Option 3: Accept the risk (stopping scenario)

---

### ERP022: Exit point swallows an unobserved exception
**Count:** 2 occurrences

**Location 1:** `src/aspnetcore/Ark.Tools.AspNetCore.BasicAuthAuth0Proxy/BasicAuthAuth0ProxyMiddleware.cs` line 97
```csharp
catch
{
    // Empty catch block
}
```
**Context:** Middleware authentication - intentionally swallowing exceptions to continue processing
**Suggested Action:** 
- Option 1: Add logging: `catch (Exception ex) { _logger.Debug(ex, "Auth failed"); }`
- Option 2: Suppress if truly intentional

**Location 2:** `src/aspnetcore/Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy/BasicAuthAzureActiveDirectoryProxyMiddleware.cs` line 99
```csharp
catch (Exception)
{ }
```
**Context:** Middleware authentication - intentionally swallowing exceptions to continue processing
**Suggested Action:** 
- Option 1: Add logging: `catch (Exception ex) { _logger.Debug(ex, "Auth failed"); }`
- Option 2: Suppress if truly intentional

---

## Summary of Errors

| Rule | Count | Severity | File(s) Affected | Recommended Action |
|------|-------|----------|------------------|-------------------|
| VSTHRD110 | 2 | error | TaskExtensions.cs | Suppress or assign to `_` |
| VSTHRD002 | 1 | error | Ex.cs (NestedStartup) | Suppress (synchronous callback context) |
| ERP022 | 2 | error | BasicAuthAuth0ProxyMiddleware.cs, BasicAuthAzureActiveDirectoryProxyMiddleware.cs | Add logging or suppress |

**Total Unique Errors:** 5 locations

---

## Analyzers Set to Silent/Suggestion/None

### Currently SILENT

#### Meziantou.Analyzer
- `MA0006` - Use String.Equals instead of equality operator
- `MA0007` - Add a comma after the last value
- `MA0048` - File name must match type name
- `MA0056` - Do not call overridable members in constructor

#### VS Threading Analyzers
- None set to silent

#### ErrorProne.NET
- None set to silent

### Currently SUGGESTION

#### Meziantou.Analyzer
- `MA0004` - Use Task.ConfigureAwait
- `MA0016` - Prefer using collection abstraction instead of implementation
- `MA0051` - Method is too long
- `MA0121` - Do not overwrite parameter value

#### VS Threading Analyzers
- `VSTHRD003` - Avoid awaiting foreign Tasks
- `VSTHRD011` - Use AsyncLazy<T>
- `VSTHRD102` - Implement internal logic asynchronously
- `VSTHRD104` - Offer async option
- `VSTHRD108` - Assert thread affinity unconditionally
- `VSTHRD111` - Use ConfigureAwait(bool)
- `VSTHRD112` - Implement System.IAsyncDisposable
- `VSTHRD113` - Check for System.IAsyncDisposable

#### ErrorProne.NET
- `EPC14` - ConfigureAwait(false) is redundant
- `EPC15` - Use ConfigureAwait(false) on await
- `EPC18` - Use synchronous method instead of async when possible
- `EPC21` - Consider making struct readonly
- `EPC29` - Incorrect await inside a loop
- `EPC30` - Avoid using TaskCompletionSource without specifying TaskCreationOptions
- `EPC34` - Avoid using Task.Factory.StartNew
- `EPC37` - Do not validate arguments inside async methods
- `ERP023` - Possible multiple enumeration of IEnumerable

### Currently NONE (Disabled)

#### Meziantou.Analyzer
- `MA0015` - Specify the parameter name in ArgumentException
- `MA0032` - Use an overload with a CancellationToken argument
- `MA0049` - Type name should not match containing namespace

#### VS Threading Analyzers
- `VSTHRD012` - Provide JoinableTaskFactory where allowed
- `VSTHRD200` - Use "Async" suffix for async methods (moved from Meziantou, now set to none)

#### ErrorProne.NET
- None set to none

---

## Recommendations for Severity Promotion

### Should Promote to WARNING

1. **MA0004** (Task.ConfigureAwait) - Currently suggestion → warning
   - Helps avoid deadlocks in library code
   - Low noise, high value

2. **VSTHRD111** (Use ConfigureAwait(bool)) - Currently suggestion → warning
   - Complements MA0004
   - Important for library code

3. **EPC15** (Use ConfigureAwait(false) on await) - Currently suggestion → warning
   - Same rationale as above
   - Helps avoid context capture issues

4. **ERP023** (Possible multiple enumeration) - Currently suggestion → warning
   - Common performance pitfall
   - Easy to fix when found

### Should Promote to ERROR

1. **VSTHRD003** (Avoid awaiting foreign Tasks) - Currently suggestion → error
   - Can cause serious threading issues
   - Critical for correctness

### Should Keep as SUGGESTION (High Noise)

- `MA0016` - Prefer collection abstraction (design choice, not error)
- `MA0051` - Method is too long (style preference)
- `MA0121` - Do not overwrite parameter value (style preference)
- `EPC14` - ConfigureAwait(false) redundant (informational)
- `EPC18` - Use synchronous method (performance optimization)
- `EPC21` - Consider making struct readonly (optimization)
- `EPC29` - Incorrect await inside loop (context-dependent)
- `EPC30` - TaskCompletionSource options (best practice)
- `EPC34` - Avoid Task.Factory.StartNew (preference)
- `EPC37` - Do not validate arguments in async (advanced pattern)

### Should Keep DISABLED

- `MA0015` - Parameter name in ArgumentException (noisy, low value)
- `MA0032` - CancellationToken overload (too aggressive, many false positives)
- `MA0049` - Type name should not match namespace (design choice)
- `VSTHRD012` - JoinableTaskFactory (Visual Studio specific)
- `VSTHRD200` - "Async" suffix (already enforced elsewhere, disabled to avoid conflicts)

---

## Configuration Changes Made

1. ✅ Moved `VSTHRD200` from `.meziantou.globalconfig` to `.vsthreading.globalconfig`
2. ✅ Set `VSTHRD200` severity to `none` (disabled as requested)

---

## Next Steps

1. Review the 5 active errors and decide:
   - Which to fix
   - Which to suppress with `#pragma warning disable`
   - Which to disable by changing severity to `none` in globalconfig

2. Consider promoting the recommended rules from suggestion to warning

3. Update `.vsthreading.globalconfig`, `.errorprone.globalconfig`, and `.meziantou.globalconfig` as needed

---

## Sample Projects Configuration

The new analyzers have been added to both sample project directories:

### Ark.ReferenceProject (`samples/Ark.ReferenceProject/`)
- ✅ Added `Microsoft.VisualStudio.Threading.Analyzers` v17.14.15
- ✅ Added `ErrorProne.NET.CoreAnalyzers` v0.1.2
- ✅ Added package versions to `Directory.Packages.props`
- ✅ Updated `Directory.Build.props` with PackageReferences and GlobalAnalyzerConfigFiles
- ✅ Copied `.vsthreading.globalconfig` and `.errorprone.globalconfig` files

### Ark.ResourceWatcher (`samples/Ark.ResourceWatcher/`)
- ✅ Added `Microsoft.VisualStudio.Threading.Analyzers` v17.14.15
- ✅ Added `ErrorProne.NET.CoreAnalyzers` v0.1.2
- ✅ Added package versions to `Directory.Packages.props`
- ✅ Updated `Directory.Build.props` with PackageReferences and GlobalAnalyzerConfigFiles
- ✅ Copied `.vsthreading.globalconfig` and `.errorprone.globalconfig` files

**Result:** No new errors detected in sample projects. All 5 errors remain in main source code only.
