# Migration to Ark.Tools v6

## Ark.Tools.Core.Reflection Merged into Core

In v5, reflection utilities were split into a separate `Ark.Tools.Core.Reflection` package. In v6, this split has been reversed - the reflection utilities are now included directly in `Ark.Tools.Core` with proper trimming annotations.

### What Changed

The separate `Ark.Tools.Core.Reflection` package no longer exists. All reflection utilities are now part of `Ark.Tools.Core` in the `Ark.Tools.Core.Reflection` namespace.

**Key improvements in v6:**
- All reflection methods use `[RequiresUnreferencedCode]` to warn about trimming implications
- `Ark.Tools.Core` is fully trimmable (builds with zero warnings)
- Simpler package structure (one package instead of two)
- 100% backward compatible - same namespace, no code changes required

### Migration Steps

#### 1. Remove the Core.Reflection Package Reference

**Before (v5):**
```xml
<ItemGroup>
  <PackageReference Include="Ark.Tools.Core" Version="5.x.x" />
  <PackageReference Include="Ark.Tools.Core.Reflection" Version="5.x.x" />
</ItemGroup>
```

**After (v6):**
```xml
<ItemGroup>
  <PackageReference Include="Ark.Tools.Core" Version="6.x.x" />
  <!-- Core.Reflection is no longer a separate package - functionality is in Core -->
</ItemGroup>
```

#### 2. No Code Changes Required

Your existing code continues to work unchanged. The namespace `Ark.Tools.Core.Reflection` is preserved:

```csharp
using Ark.Tools.Core.Reflection;

// All existing code works as before
var table = myData.ToDataTablePolymorphic();
var itemType = ReflectionHelper.GetEnumerableItemType(typeof(List<string>));
```

#### 3. Expect New Trim Warnings (This is Correct Behavior)

When using reflection features, you'll now see trim warnings. This is **expected and correct** - it alerts you that these methods require unreferenced code:

```csharp
// Using reflection methods will show IL2026 warnings
var table = items.ToDataTablePolymorphic(); // IL2026: requires unreferenced code
```

**These warnings are informational** - they help you understand which parts of your code may not work correctly when trimming is enabled.

#### 4. Suppressing Warnings (If Needed)

If you're not using trimming, or if you know your types will be preserved, you can suppress the warnings:

```csharp
using System.Diagnostics.CodeAnalysis;

[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "We don't use trimming in this application")]
public void ProcessData()
{
    var table = items.ToDataTablePolymorphic();
}
```

Or suppress at the call site:
```csharp
#pragma warning disable IL2026
var table = items.ToDataTablePolymorphic();
#pragma warning restore IL2026
```

### Available Reflection Utilities

All previously available utilities are still available in `Ark.Tools.Core.Reflection`:

- **ReflectionHelper**: Type introspection utilities
- **ToDataTablePolymorphic()**: Polymorphic DataTable conversion
- **OrderBy(string)**: String-based LINQ OrderBy for IQueryable
- **DynamicTypeAssembly**: Dynamic type generation using Reflection.Emit

### Benefits of the Merge

1. **Simpler dependency management** - Only need to reference `Ark.Tools.Core`
2. **Better trimming support** - Core is now 100% trimmable
3. **Clear warnings** - Users get appropriate warnings when using reflection features
4. **Backward compatible** - No code changes required
5. **Same functionality** - All features remain available

### Troubleshooting

**Q: I'm seeing IL2026 warnings after upgrading. Is this a problem?**

A: No, this is expected and correct behavior. The warnings inform you that these methods use reflection. If you're not using trimming, you can safely suppress them. If you are using trimming, review each warning to ensure the types being reflected upon are preserved.

**Q: Do I need to change my code?**

A: No. All existing code continues to work unchanged. Just remove the `Ark.Tools.Core.Reflection` package reference.

**Q: Can I still use reflection utilities with trimming enabled?**

A: Yes, but you need to ensure the types you're reflecting on are preserved. Use `[DynamicallyAccessedMembers]` or `[DynamicDependency]` attributes, or disable trimming for specific assemblies.

**Q: What if I need the old Core.Reflection package?**

A: The v5.x version of `Ark.Tools.Core.Reflection` remains available on NuGet. However, we recommend migrating to v6 for better trimming support and simpler package management.

## .NET 10.0 Support

.NET SDK has been updated to .NET 10. All packages now target both .NET 8.0 (LTS) and .NET 10.0.

## Trimming Support

All 61 libraries in Ark.Tools are now trimmable with zero build warnings. Libraries that use reflection expose this through `[RequiresUnreferencedCode]` attributes, allowing you to make informed decisions about trimming compatibility.

For more information, see the [Trimming Guidelines](trimmable-support/guidelines.md).
