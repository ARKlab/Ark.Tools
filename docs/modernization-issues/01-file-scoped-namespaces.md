# Issue: Migrate to File-Scoped Namespaces

## Overview

**Priority:** High  
**Impact:** Medium - Improves readability, reduces indentation  
**Effort:** Low (automated via IDE)  
**Analyzer Support:** IDE0160/IDE0161

## Description

Migrate all C# files from block-scoped namespaces to file-scoped namespaces (C# 10+ feature). This reduces indentation by one level and improves code readability.

## Current State

All `.cs` files currently use block-scoped namespaces:
```csharp
// Copyright header
namespace Ark.Tools.Core
{
    public static class EnumerableExtensions
    {
        // 4-space indent
        public static IEnumerable<T> OrderBy<T>(...)
        {
            // 8-space indent
        }
    }
}
```

**Analyzer Configuration:** `.editorconfig` currently has:
```
csharp_style_namespace_declarations = block_scoped:warning
```

## Target State

All files should use file-scoped namespaces:
```csharp
// Copyright header
namespace Ark.Tools.Core;

public static class EnumerableExtensions
{
    // 0-space indent at top level
    public static IEnumerable<T> OrderBy<T>(...)
    {
        // 4-space indent
    }
}
```

## Implementation Steps

### Step 1: Update Analyzer Configuration
1. Open `.editorconfig`
2. Change: `csharp_style_namespace_declarations = block_scoped:warning`
3. To: `csharp_style_namespace_declarations = file_scoped:warning`
4. Save and commit

### Step 2: Apply to All Files
1. Use IDE (Visual Studio/Rider) bulk refactoring:
   - Option 1: Use "Convert to file-scoped namespace" quick action on each file
   - Option 2: Use IDE's "Apply refactoring to project/solution" feature
2. Or use command-line tool if available
3. Review changes to ensure no issues

### Step 3: Build and Test
1. Run `dotnet build --no-restore` to ensure all projects compile
2. Fix any build errors (should be none for this change)
3. Run full test suite: `dotnet test`
4. Verify all tests pass

## Verification Steps

- [ ] `.editorconfig` updated to `file_scoped:warning`
- [ ] All `.cs` files in `src/` directory converted to file-scoped namespaces
- [ ] All `.cs` files in `samples/` directory converted to file-scoped namespaces
- [ ] All `.cs` files in `tests/` directory converted to file-scoped namespaces
- [ ] Solution builds successfully: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] No new analyzer warnings introduced
- [ ] Code review confirms consistent formatting

## Benefits

- Reduces indentation levels across entire codebase
- More screen real estate for actual code
- Consistent with modern C# conventions
- Easier to read and maintain
- Supported by all .NET 8+ and .NET 10 projects

## Risks

**Low Risk** - This is a purely cosmetic change that doesn't affect runtime behavior.

## Dependencies

None - can be implemented independently.

## References

- [Microsoft Docs: File-scoped namespaces](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-10.0/file-scoped-namespaces)
- [Full Modernization Plan](./README.md#13-migrate-to-file-scoped-namespaces)
