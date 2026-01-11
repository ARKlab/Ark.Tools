# Trimming Compatibility - Ark.Tools.Core

## Status: ❌ NOT TRIMMABLE

**Decision Date:** 2026-01-11  
**Rationale:** Fundamentally relies on runtime reflection that cannot be statically analyzed

---

## Why This Library Is Not Trimmable

Ark.Tools.Core is a foundational utility library that provides extensive reflection-based functionality. Making this library trim-compatible would require either:
1. Breaking changes that remove core functionality, OR
2. Massive refactoring that would fundamentally change the library's design

The cost and risk significantly outweigh the benefits.

---

## Technical Analysis

### Warning Summary

When `IsTrimmable` and `EnableTrimAnalyzer` are enabled, the library generates **88 trim warnings** across multiple files:

| File | Warnings | Issue Type |
|------|----------|------------|
| **ShredObjectToDataTable.cs** | 32 | Reflects over object properties/fields to generate DataTables |
| **EnumerableExtensions.cs** | 24 | LINQ Queryable extensions require expression tree analysis |
| **ReflectionHelper.cs** | 16 | Reflection utilities by design |
| **DynamicTypeAssembly.cs** | 8 | Runtime type creation via Reflection.Emit |
| **EnumExtensions.cs** | 4 | Enum field reflection |
| **DataKeyPrinter/Comparer.cs** | 4 | Property reflection for data keys |

### Warning Types

- **IL2026**: RequiresUnreferencedCode (Queryable.AsQueryable methods)
- **IL2060**: MakeGenericMethod cannot be statically analyzed
- **IL2067**: TypeBuilder.SetParent requires DynamicallyAccessedMembers.All
- **IL2070**: GetInterfaces/GetConstructors/GetFields/GetProperties missing annotations
- **IL2072**: DataColumnCollection.Add type parameter issues
- **IL2075**: Object.GetType() return value lacks annotations
- **IL2080**: Field doesn't satisfy annotation requirements
- **IL2087**: Generic parameter doesn't satisfy annotation
- **IL2090**: Generic parameter doesn't satisfy annotation for reflection

---

## Core Reflection-Heavy Features

### 1. **ShredObjectToDataTable&lt;T&gt;** - Object to DataTable Conversion

**Purpose:** Converts objects to DataTables using reflection over properties and fields

**Why Not Trimmable:**
- Uses `Type.GetFields()` and `Type.GetProperties()` to discover object structure at runtime
- Supports arbitrary object types with no compile-time knowledge
- Dynamically creates DataTable columns based on reflected types
- Recursively processes nested object types

**Code Pattern:**
```csharp
var fields = _type.GetFields();  // IL2080
var properties = _type.GetProperties();  // IL2080
// Dynamically adds columns for each field/property
```

**Alternative:** Would require explicit schema registration for every type, breaking the utility's purpose

---

### 2. **LINQ Queryable Extensions** - IQueryable&lt;T&gt; Helpers

**Purpose:** Extends IEnumerable&lt;T&gt; with AsQueryable and expression-based filtering

**Why Not Trimmable:**
- `Queryable.AsQueryable()` requires runtime expression tree compilation
- .NET's trim analyzer explicitly warns about IQueryable due to expression rebinding
- `MakeGenericMethod` for dynamic query compilation cannot be statically analyzed

**Code Pattern:**
```csharp
return items.AsQueryable();  // IL2026 - Expression trees require unreferenced code
query.Provider.Execute(expression);  // IL2060 - MakeGenericMethod
```

**Microsoft's Warning:**
> "Enumerating in-memory collections as IQueryable can require unreferenced code because expressions referencing IQueryable extension methods can get rebound to IEnumerable extension methods. The IEnumerable extension methods could be trimmed causing the application to fail at runtime."

**Alternative:** Would require removing all IQueryable support, breaking LINQ provider scenarios

---

### 3. **ReflectionHelper** - Type Introspection Utilities

**Purpose:** Provides helper methods for type analysis (interfaces, properties, fields)

**Why Not Trimmable:**
- `GetInterfaces()` to find compatible generic interfaces
- `GetField(name)` and `GetProperty(name)` for dynamic member access
- Uses `Object.GetType()` with reflection operations on unknown types

**Code Pattern:**
```csharp
public static Type? GetCompatibleGenericInterface(Type type, Type genericInterface)
{
    return type.GetInterfaces()  // IL2070
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface);
}
```

**Alternative:** Would require removing these utilities, breaking dependent libraries

---

### 4. **DynamicTypeAssembly** - Runtime Type Creation

**Purpose:** Creates types at runtime using Reflection.Emit

**Why Not Trimmable:**
- Uses `TypeBuilder.SetParent()` with arbitrary parent types
- Calls `GetConstructors()` on unknown types
- Dynamically emits IL for new types

**Code Pattern:**
```csharp
TypeBuilder typeBuilder = ...;
typeBuilder.SetParent(parentType);  // IL2067 - Requires DynamicallyAccessedMembers.All
var constructors = parentType.GetConstructors(...);  // IL2070
```

**Alternative:** Would require compile-time type generation (source generators), fundamentally changing design

---

### 5. **DataKeyComparer/DataKeyPrinter** - Generic Data Key Handling

**Purpose:** Compares and prints data key objects using [DataKey] attributed properties

**Why Not Trimmable:**
- Reflects over generic type `T` to find properties with `[DataKey]` attribute
- Runtime discovery of key properties

**Code Pattern:**
```csharp
public class DataKeyComparer<T>
{
    private static readonly PropertyInfo[] _keyProperties = 
        typeof(T).GetProperties()  // IL2090
            .Where(p => p.GetCustomAttribute<DataKeyAttribute>() != null)
            .ToArray();
}
```

**Alternative:** Would require explicit registration of data key properties for each type

---

## Impact on Dependent Libraries

Despite Ark.Tools.Core being non-trimmable, **35 out of 42 common libraries (83%) have been made trimmable**. 

### How Dependent Libraries Can Still Be Trimmable

Libraries that depend on Ark.Tools.Core can still be marked as trimmable if:
1. They don't call the reflection-heavy methods at runtime
2. They only use simple utilities (constants, extensions that don't use reflection)
3. They properly annotate their own reflection usage

**Examples of Trimmable Libraries Using Ark.Tools.Core:**
- **Ark.Tools.Nodatime** - Uses only NodaTime types, no reflection
- **Ark.Tools.Sql** - Uses simple utilities
- **Ark.Tools.Outbox** - Uses entity types, no dynamic reflection
- **Ark.Tools.EventSourcing** - Uses annotated reflection with `DynamicallyAccessedMembers`

---

## Recommendations for Users

### For Trimmed Applications

If you're deploying a trimmed application:

1. **✅ Safe to Use:** Simple utilities that don't use reflection
   - Constants (`ArkToolsConstants`)
   - Basic extensions (`CollectionExtensions`, `TaskExtensions`, `EnumerableExtensions` non-Queryable methods)
   - Value types (`ValueObject`, `BusinessRuleViolation`)
   - Exceptions (`EntityNotFoundException`, `OptimisticConcurrencyException`)

2. **⚠️ Use With Caution:** Reflection-based utilities
   - Ensure types you pass to these methods are preserved
   - Use `[DynamicallyAccessedMembers]` attributes on type parameters
   - Test thoroughly with actual trim analysis

3. **❌ Avoid in Trimmed Apps:**
   - `ShredObjectToDataTable<T>` - Heavy reflection usage
   - `DynamicTypeAssembly` - Runtime type creation
   - `Queryable` extensions - Expression tree compilation
   - `ReflectionHelper` - Unless you preserve the types being reflected

### Preserving Ark.Tools.Core in Trimmed Apps

If you need to use reflection-heavy features, add to your `.csproj`:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="Ark.Tools.Core" />
</ItemGroup>
```

**Warning:** This will prevent trimming of the entire Ark.Tools.Core assembly, negating some benefits of trimming.

---

## Alternative Approaches Considered

### ❌ Library Splitting

**Proposal:** Split into `Ark.Tools.Core` (trimmable) and `Ark.Tools.Core.Reflection` (not trimmable)

**Why Not:**
- Many files mix simple and reflection-based code
- Would require breaking changes to existing consumers
- Effort cost: 40-60 hours estimated
- Benefits: Marginal - most dependent libraries already trimmable
- Risk: High - could break existing applications

### ❌ Massive Annotation Effort

**Proposal:** Add `[DynamicallyAccessedMembers]` attributes throughout

**Why Not:**
- Would require 88+ annotations across 7 files
- Many patterns cannot be fully annotated (Object.GetType(), dynamic types)
- Generic type creation (DynamicTypeAssembly) requires DynamicallyAccessedMembers.All
- Effort cost: 40-60 hours estimated
- Risk: High - incorrect annotations could hide bugs

### ❌ Remove Reflection Features

**Proposal:** Remove reflection-heavy utilities

**Why Not:**
- Breaking changes for existing users
- Removes core value proposition of the library
- Would require major version bump (v7.0)
- No migration path for existing code

---

## Conclusion

Ark.Tools.Core provides valuable reflection-based utilities that are fundamentally incompatible with trimming. The library is **intentionally marked as not trimmable** to:

1. **Preserve functionality** for existing users
2. **Avoid breaking changes**
3. **Focus trimming effort** on higher-value libraries

The **83% of libraries that are now trimmable** demonstrates that this decision does not block the overall trimming initiative. Applications using trim-compatible Ark.Tools libraries will still benefit significantly from reduced deployment sizes.

---

## References

- [Trimming Progress Tracker](../../../docs/trimmable-support/progress-tracker.md)
- [Trimming Implementation Plan](../../../docs/trimmable-support/implementation-plan.md)
- [Microsoft: Prepare libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [IL Warning Codes](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings)
