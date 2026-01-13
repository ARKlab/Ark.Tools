# Trimming Compatibility - Ark.Tools.Core.Reflection

## Status: ❌ NOT TRIMMABLE

**Decision Date:** 2026-01-11  
**Rationale:** Contains reflection-based utilities that fundamentally rely on runtime type discovery

---

## Overview

This library was split from `Ark.Tools.Core` to isolate reflection-heavy utilities that are incompatible with assembly trimming. The base `Ark.Tools.Core` library is now **fully trimmable** ✅, while this library contains the reflection-based features.

### Library Split

- **Ark.Tools.Core** ✅ - Trimmable (0 warnings)
  - Basic extensions, utilities, value objects
  - Business exceptions, async helpers
  - Email validator, collection extensions
  
- **Ark.Tools.Core.Reflection** ❌ - NOT Trimmable (this library)
  - ShredObjectToDataTable
  - LINQ Queryable extensions
  - ReflectionHelper utilities

---

## Why This Library Is Not Trimmable

This library contains utilities that are designed to work with runtime-determined types using reflection. When trimming is enabled, these utilities would produce **88+ trim warnings** because the .NET trimmer cannot statically analyze which types will be used.

### Technical Analysis

#### 1. **ShredObjectToDataTable&lt;T&gt;** - Object to DataTable Conversion

**Location:** `ShredObjectToDataTable.cs`  
**Warning Types:** IL2026, IL2070, IL2075

```csharp
// Reflects over object properties at runtime
Type.GetFields(BindingFlags.Public | BindingFlags.Instance)
Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
```

**Why Not Trimmable:** Dynamically discovers object structure at runtime to create DataTable columns. The trimmer cannot know which types will be used with this method, so it cannot preserve the necessary field/property metadata.

#### 2. **LINQ Queryable Extensions** - IQueryable Support

**Location:** `EnumerableExtensions.cs`  
**Warning Types:** IL2026

```csharp
// AsQueryable requires expression tree compilation
IQueryable.AsQueryable() // Marked with RequiresUnreferencedCode by Microsoft
```

**Why Not Trimmable:** Expression trees require runtime code generation and type metadata that the trimmer cannot preserve. Microsoft explicitly marks `AsQueryable` as not trim-safe.

#### 3. **ReflectionHelper** - Type Introspection Utilities

**Location:** `ReflectionHelper.cs`  
**Warning Types:** IL2070, IL2067, IL2090

```csharp
// Type introspection by design
typeof(T).GetInterfaces()
typeof(T).GetMethods()
GetCompatibleGenericInterface(Type type, Type genericInterface)
```

**Why Not Trimmable:** These are reflection utilities designed for runtime type discovery. Making them trimmable would defeat their purpose.

#### 4. **DataTable Extensions** - Property Reflection

**Location:** `DataTableExtensions.cs`  
**Warning Types:** IL2067, IL2072

```csharp
// Reflects over properties at runtime
typeof(T).GetProperties()
```

**Why Not Trimmable:** Scans generic type T for properties at runtime. The trimmer cannot know which types will be used.

---

## Impact on Applications

### ✅ Most Applications Don't Need This Library

The majority of Ark.Tools usage scenarios **do not require** the reflection utilities in this library:

**Common Scenarios (Trimmable):**
- ✅ Using Ark.Tools.Nodatime for date/time handling
- ✅ Using Ark.Tools.Sql for database access
- ✅ Using Ark.Tools.NLog for logging
- ✅ Using Ark.Tools.Http for HTTP clients
- ✅ Using Ark.Tools.EventSourcing for event sourcing
- ✅ Using business exceptions and value objects from Core

**These all use `Ark.Tools.Core` (trimmable) and do NOT require `Ark.Tools.Core.Reflection`.**

### ❌ Scenarios Requiring This Library (Not Trimmable)

Only use this library if you need:
- ShredObjectToDataTable for DataTable generation
- IQueryable.AsQueryable() extensions
- ReflectionHelper for type introspection

---

## Recommendations

### For Applications Using Trimming

If your application uses trimming:

1. **Avoid This Library:**
   - Don't reference `Ark.Tools.Core.Reflection`
   - Don't use `ShredObjectToDataTable<T>`
   - Don't use `ReflectionHelper` utilities
   - Avoid `IQueryable.AsQueryable()` extension methods

2. **Use Alternatives:**
   - **Object Mapping:** Use AutoMapper, Mapster, or source generators instead of ShredObjectToDataTable
   - **Type Discovery:** Use explicit registration instead of ReflectionHelper
   - **DataTable Conversion:** Use typed DTOs with source-generated serialization
   - **Queryable:** Use `DbSet<T>` or `IQueryable<T>` directly without AsQueryable() extensions

3. **Verify Dependencies:**
   ```bash
   # Check if your project references Core.Reflection
   dotnet list package | grep Core.Reflection
   ```

### For Applications Requiring Reflection Features

If you must use the reflection utilities:

1. **Disable Trimming:**
   Remove `<PublishTrimmed>true</PublishTrimmed>` from your project file

2. **Preserve the Assembly:**
   Add to your `.csproj`:
   ```xml
   <ItemGroup>
     <TrimmerRootAssembly Include="Ark.Tools.Core.Reflection" />
   </ItemGroup>
   ```
   
   **Warning:** This preserves the entire assembly, losing trimming benefits.

3. **Consider Migration:**
   - Plan to migrate away from reflection-based utilities
   - Use strongly-typed alternatives where possible
   - Evaluate if the reflection features are truly necessary

---

## Alternative Approaches Considered

### ✅ Library Splitting (COMPLETED)

**Implemented:** Split Core into trimmable and non-trimmable parts

**Result:**
- ✅ `Ark.Tools.Core` is now fully trimmable (0 warnings)
- ❌ `Ark.Tools.Core.Reflection` contains reflection utilities (not trimmable)
- ✅ 35/42 common libraries remain trimmable
- ✅ Applications can use Core without Reflection and achieve full trimming

**Benefits:**
- Clear separation of concerns
- Applications can choose trimming support vs reflection features
- No breaking changes (existing code continues to work)
- Most usage scenarios don't need reflection features

### ❌ Massive Annotation Effort

**Considered:** Add attributes to make reflection code trim-safe

**Why Not:**
- Runtime type discovery cannot be made statically analyzable
- Suppressions would hide real runtime failures
- No practical benefit - reflection features inherently incompatible

### ❌ Remove Reflection Features

**Considered:** Remove reflection-based utilities entirely

**Why Not:**
- Breaking changes for existing consumers
- Some scenarios legitimately need reflection
- Better to offer choice (Core vs Core.Reflection)

---

## Migration Guide

### From Ark.Tools.Core to Ark.Tools.Core + Ark.Tools.Core.Reflection

**No Action Required** - The split is backward compatible:

1. **If you don't use reflection features:**
   - Your project already only uses `Ark.Tools.Core` (trimmable)
   - No changes needed

2. **If you use ShredObjectToDataTable, ReflectionHelper, or AsQueryable():**
   - Add explicit reference to `Ark.Tools.Core.Reflection`
   - Your code continues to work unchanged
   - Understand that trimming is not supported

### Example Package Reference

```xml
<ItemGroup>
  <!-- Trimmable - for most scenarios -->
  <PackageReference Include="Ark.Tools.Core" Version="X.Y.Z" />
  
  <!-- Only if you need reflection features -->
  <PackageReference Include="Ark.Tools.Core.Reflection" Version="X.Y.Z" />
</ItemGroup>
```

---

## Conclusion

Ark.Tools.Core.Reflection is **intentionally marked as not trimmable** because:

1. **Contains Reflection Utilities:** Designed for runtime type discovery by design
2. **Library Split Completed:** Trimmable code moved to Ark.Tools.Core ✅
3. **Clear Choice:** Applications choose trimming (Core) vs reflection (Core.Reflection)
4. **Minimal Impact:** Most scenarios don't need reflection features

### Impact Summary

- **Ark.Tools.Core:** ✅ Trimmable (0 warnings) - Use for most scenarios
- **Ark.Tools.Core.Reflection:** ❌ Not Trimmable - Only if you need reflection features
- **Common Libraries:** 35/42 (83%) remain trimmable ✅
- **Applications:** Can achieve **30-40% size reduction** by using Core without Reflection

The library split successfully delivers both **trimming support** for modern applications and **backward compatibility** for applications using reflection features.

---

## References

- [Microsoft: Trimming Warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings)
- [Microsoft: Prepare Libraries for Trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [Ark.Tools.Core](../Ark.Tools.Core/) - Trimmable ✅
- [Trimming Progress Tracker](../../../docs/trimmable-support/progress-tracker.md)
