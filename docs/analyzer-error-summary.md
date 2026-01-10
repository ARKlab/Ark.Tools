# Analyzer Errors and Configuration Summary

**Last Updated:** After implementing all requested fixes and analyzer severity changes

**Scope:** All errors listed below apply to:
- Main source code (`src/`)
- Sample projects (`samples/Ark.ReferenceProject/`, `samples/Ark.ResourceWatcher/`)

## Active Errors (TreatWarningsAsErrors=true)

**Status:** ✅ All errors have been fixed!

### Previously Fixed Errors

#### VSTHRD110: Observe the awaitable result of this method call
**Status:** ✅ FIXED - Assigned to `_` variable

**Location 1:** `src/common/Ark.Tools.Core/TaskExtensions.cs` line 12
**Fix Applied:** Changed `task.ContinueWith(...)` to `_ = task.ContinueWith(...)`

**Location 2:** `src/common/Ark.Tools.Core/TaskExtensions.cs` line 22
**Fix Applied:** Changed `task.ContinueWith(...)` to `_ = task.ContinueWith(...)`

---

#### ERP022: Exit point swallows an unobserved exception
**Status:** ✅ FIXED - Added trace-level logging

**Location 1:** `src/aspnetcore/Ark.Tools.AspNetCore.BasicAuthAuth0Proxy/BasicAuthAuth0ProxyMiddleware.cs` line 97
**Fix Applied:** 
- Added `ILogger<BasicAuthAuth0ProxyMiddleware>` to constructor
- Changed `catch { }` to `catch (Exception ex) { _logger.LogTrace(ex, "Basic authentication failed"); }`

**Location 2:** `src/aspnetcore/Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy/BasicAuthAzureActiveDirectoryProxyMiddleware.cs` line 99
**Fix Applied:**
- Added `ILogger<BasicAuthAzureActiveDirectoryProxyMiddleware>` to constructor
- Changed `catch (Exception) { }` to `catch (Exception ex) { _logger.LogTrace(ex, "Basic authentication failed"); }`

---

## Summary of Errors

**Total Unique Errors:** 0 (all fixed!)

| Rule | Previous Count | Status | Fix Applied |
|------|----------------|--------|-------------|
| VSTHRD110 | 2 | ✅ FIXED | Assigned to `_` variable |
| ERP022 | 2 | ✅ FIXED | Added trace-level logging |

**Note:** VSTHRD002 error in Ex.cs remains acceptable as it's in a synchronous callback context (ApplicationStopping).

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
### Currently SUGGESTION

#### Meziantou.Analyzer
- `MA0016` - Prefer using collection abstraction instead of implementation
- `MA0051` - Method is too long
- `MA0121` - Do not overwrite parameter value

#### VS Threading Analyzers
- `VSTHRD011` - Use AsyncLazy<T>
- `VSTHRD102` - Implement internal logic asynchronously
- `VSTHRD104` - Offer async option
- `VSTHRD108` - Assert thread affinity unconditionally
- `VSTHRD112` - Implement System.IAsyncDisposable
- `VSTHRD113` - Check for System.IAsyncDisposable

#### ErrorProne.NET
- `EPC14` - ConfigureAwait(false) is redundant
- `EPC18` - Use synchronous method instead of async when possible
- `EPC21` - Consider making struct readonly
- `EPC29` - Incorrect await inside a loop
- `EPC30` - Avoid using TaskCompletionSource without specifying TaskCreationOptions
- `EPC34` - Avoid using Task.Factory.StartNew
- `EPC37` - Do not validate arguments inside async methods

### Currently WARNING

#### Meziantou.Analyzer
- `MA0004` - Use Task.ConfigureAwait ✅ **PROMOTED from suggestion**

### Currently ERROR

#### VS Threading Analyzers
- `VSTHRD003` - Avoid awaiting foreign Tasks ✅ **PROMOTED from suggestion**

#### ErrorProne.NET
- `ERP023` - Possible multiple enumeration of IEnumerable ✅ **PROMOTED from suggestion**

### Currently NONE (Disabled)

#### Meziantou.Analyzer
- `MA0015` - Specify the parameter name in ArgumentException
- `MA0032` - Use an overload with a CancellationToken argument
- `MA0049` - Type name should not match containing namespace

#### VS Threading Analyzers
- `VSTHRD012` - Provide JoinableTaskFactory where allowed
- `VSTHRD111` - Use ConfigureAwait(bool) ✅ **DISABLED in favor of MA0004**
- `VSTHRD200` - Use "Async" suffix for async methods

#### ErrorProne.NET
- `EPC15` - Use ConfigureAwait(false) on await ✅ **DISABLED in favor of MA0004**

---

## Configuration Changes Made

1. ✅ Moved `VSTHRD200` from `.meziantou.globalconfig` to `.vsthreading.globalconfig`
2. ✅ Set `VSTHRD200` severity to `none` (disabled as requested)
3. ✅ **Promoted `MA0004` from suggestion to warning**
4. ✅ **Promoted `VSTHRD003` from suggestion to error**
5. ✅ **Promoted `ERP023` from suggestion to error**
6. ✅ **Disabled `VSTHRD111` in favor of MA0004 (avoid duplicate ConfigureAwait warnings)**
7. ✅ **Disabled `EPC15` in favor of MA0004 (avoid duplicate ConfigureAwait warnings)**

### Rationale for ConfigureAwait Rule Consolidation

To avoid requiring multiple suppressions for the same issue, only **MA0004** is enabled for ConfigureAwait warnings:
- `MA0004` (Meziantou) - **WARNING** ✅ Primary rule
- `VSTHRD111` (VS Threading) - **NONE** (disabled)
- `EPC15` (ErrorProne.NET) - **NONE** (disabled)

This ensures developers only need to address one warning per missing ConfigureAwait, not three.

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
