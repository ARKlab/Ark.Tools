# Issue: Implement Global Usings

## Overview

**Priority:** Medium  
**Impact:** Low-Medium - Reduces boilerplate  
**Effort:** Low  
**Analyzer Support:** No specific analyzer needed

## Description

Implement global usings to reduce repetitive `using` statements across the codebase. This will be done via `Directory.Build.props` or individual project `.csproj` files rather than `GlobalUsings.cs` files.

## Current State

Every C# file contains repetitive using statements:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
```

## Target State

Common namespaces declared once per project in `.csproj` or `Directory.Build.props`:

```xml
<!-- In Directory.Build.props or specific project .csproj -->
<ItemGroup>
  <Using Include="System" />
  <Using Include="System.Collections.Generic" />
  <Using Include="System.Linq" />
  <Using Include="System.Threading" />
  <Using Include="System.Threading.Tasks" />
</ItemGroup>
```

## Implementation Steps

### Step 1: Identify Common Usings Per Project Category

Analyze `src/common/`, `src/aspnetcore/`, `src/resourcewatcher/` separately to find most common namespaces.

**Common Namespaces (likely for all projects):**
- System
- System.Collections.Generic
- System.Linq
- System.Threading
- System.Threading.Tasks

**AspNetCore Projects:**
- Microsoft.AspNetCore.Http
- Microsoft.AspNetCore.Mvc
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging

### Step 2: Add to Directory.Build.props (Option 1 - Recommended)

Create or update `Directory.Build.props` in `src/common/`, `src/aspnetcore/`, `src/resourcewatcher/`:

```xml
<Project>
  <ItemGroup>
    <!-- Common usings for this category -->
    <Using Include="System" />
    <Using Include="System.Collections.Generic" />
    <Using Include="System.Linq" />
    <Using Include="System.Threading" />
    <Using Include="System.Threading.Tasks" />
  </ItemGroup>
</Project>
```

### Step 3: Add to Individual .csproj (Option 2 - If fine-grained control needed)

Add to each project's `.csproj` file as needed.

### Step 4: Remove Redundant Using Statements

Use IDE to remove now-redundant using statements from `.cs` files.

### Step 5: Build and Test

1. Build each affected project: `dotnet build`
2. Fix any missing usings that weren't made global
3. Run tests: `dotnet test`

## Verification Steps

- [ ] Analyzed common using statements per project category
- [ ] Added global usings to appropriate `Directory.Build.props` or `.csproj` files
- [ ] Removed redundant using statements from `.cs` files
- [ ] All projects build successfully: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] No increase in build warnings
- [ ] Code review confirms appropriate namespaces are global
- [ ] Domain-specific namespaces remain explicit (not global)

## Benefits

- Reduces repetitive using statements
- Clearer focus on domain-specific imports
- Easier to maintain consistent imports across projects
- Can improve compile times in some cases

## Risks

**Low Risk** - Easy to roll back by removing global usings.

**Considerations:**
- Don't overuse - only truly common namespaces should be global
- Keep domain-specific imports explicit for clarity
- Document in project README which usings are global

## Dependencies

None - can be implemented independently. Works well after file-scoped namespaces (#01).

## References

- [Microsoft Docs: Global using directives](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive#global-modifier)
- [Full Modernization Plan](../modernization-plan-net10.md#22-implement-global-usings)
