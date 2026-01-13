# Trimming Compatibility - Ark.Tools.Core

## Status: ❌ NOT TRIMMABLE

**Decision Date:** 2026-01-11  
**Rationale:** Fundamentally relies on runtime reflection that cannot be statically analyzed

---

## Why This Library Is Not Trimmable

When trimming is enabled on Ark.Tools.Core, the build produces **88 trim warnings** across 7 files. The library contains fundamental reflection-based utilities that are designed to work with runtime-determined types, making them incompatible with the static analysis requirements of the .NET trimmer.

### Technical Analysis

#### 1. **ShredObjectToDataTable&lt;T&gt;** - Object to DataTable Conversion

**Location:** `DataTableExtensions.cs`  
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

#### 4. **DynamicTypeAssembly** - Runtime Type Creation

**Location:** `DataKey/DynamicTypeAssembly.cs`  
**Warning Types:** IL2026, IL2080, IL2087

```csharp
// Runtime type creation via Reflection.Emit
AssemblyBuilder.DefineDynamicAssembly()
TypeBuilder.CreateType()
```

**Why Not Trimmable:** Creates types at runtime using Reflection.Emit. The trimmer cannot know what types will be created, so it cannot preserve the necessary metadata.

#### 5. **DataKey Utilities** - Property Reflection

**Location:** `DataKey/DataKeyComparer.cs`, `DataKey/DataKeyPrinter.cs`  
**Warning Types:** IL2067, IL2072

```csharp
// Reflects over properties to find [DataKey] attributes
typeof(T).GetProperties()
    .Where(p => p.GetCustomAttribute<DataKeyAttribute>() != null)
```

**Why Not Trimmable:** Scans generic type T for properties with custom attributes at runtime. The trimmer cannot know which types will be used with DataKey utilities.

#### 6. **Enum Extensions** - Enum Field Reflection

**Location:** `EnumExtensions.cs`  
**Warning Types:** IL2067, IL2072

```csharp
// Reflects over enum fields
enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
```

**Why Not Trimmable:** Dynamically discovers enum values and attributes at runtime.

---

## Warning Summary

When `IsTrimmable` and `EnableTrimAnalyzer` are enabled, Ark.Tools.Core produces:

- **Total Warnings:** 88 across 7 files
- **Warning Types:** IL2026, IL2060, IL2067, IL2070, IL2072, IL2075, IL2080, IL2087, IL2090
- **Affected Files:**
  - `DataTableExtensions.cs` (ShredObjectToDataTable) - 32 warnings
  - `EnumerableExtensions.cs` (LINQ Queryable) - 24 warnings
  - `ReflectionHelper.cs` - 16 warnings
  - `DataKey/DynamicTypeAssembly.cs` - 8 warnings
  - `EnumExtensions.cs` - 4 warnings
  - `DataKey/DataKeyComparer.cs` - 2 warnings
  - `DataKey/DataKeyPrinter.cs` - 2 warnings

---

## Alternative Approaches Considered

### ❌ Library Splitting (Core + Core.Reflection)

**Proposal:** Split into a trim-safe core and a reflection-heavy extension library

**Why Not:**
- **High Risk:** Reflection utilities are spread throughout the codebase
- **Marginal Benefit:** Most dependent libraries still function despite Core not being trimmable
- **High Effort:** 40-60 hours of refactoring with breaking changes
- **Uncertain ROI:** Only ~7 additional libraries might become trimmable

### ❌ Massive Annotation Effort

**Proposal:** Add `DynamicallyAccessedMembers` and `UnconditionalSuppressMessage` attributes throughout

**Why Not:**
- **88+ Annotations Required:** Across 7 files
- **Many Patterns Cannot Be Annotated:** Dynamic type creation via Reflection.Emit cannot be made trim-safe with attributes
- **Maintenance Burden:** Every new reflection-based utility would need careful annotation
- **False Sense of Safety:** Suppressions would hide real runtime failures

### ❌ Remove Reflection Features

**Proposal:** Remove reflection-based utilities from the library

**Why Not:**
- **Breaking Changes:** Many consumers depend on these utilities
- **Core Value Proposition:** Reflection utilities are fundamental to the library's purpose
- **Alternative Libraries Exist:** Consumers needing trim-safe utilities can use other libraries

---

## Impact on Dependent Libraries

Despite Ark.Tools.Core not being trimmable, **most dependent libraries can still be made trimmable** because they use non-reflection parts of Core.

### ✅ Trimmable Despite Core Dependency

These libraries depend on Ark.Tools.Core but are still trimmable:

- **Ark.Tools.Nodatime** ✅ - Uses only basic types and interfaces
- **Ark.Tools.Sql** ✅ - Uses only connection and transaction abstractions
- **Ark.Tools.Outbox** ✅ - Uses only entity interfaces
- **Ark.Tools.EventSourcing** ✅ - Uses minimal reflection with proper annotations
- **Ark.Tools.Solid** ✅ - Uses only command/query interfaces
- **Ark.Tools.NLog** ✅ - Uses only extension methods and constants
- **Ark.Tools.FtpClient.Core** ✅ - Uses only exception types
- **Ark.Tools.RavenDb** ✅ - Uses only entity interfaces
- **Ark.Tools.Rebus** ✅ - Uses only context abstractions

**Total:** 35 out of 42 common libraries (83%) are trimmable despite Core dependency!

### ❌ Not Trimmable (Require Reflection Features)

These libraries use the reflection-heavy parts of Core:

- **Ark.Tools.SystemTextJson** - Uses `ReflectionHelper` for type discovery
- **Libraries using ShredObjectToDataTable** - Direct reflection usage
- **Libraries using DataKey utilities** - Property reflection

---

## Recommendations

### For Applications Using Trimming

If your application uses trimming:

1. **Avoid Reflection-Heavy Features:**
   - Don't use `ShredObjectToDataTable<T>`
   - Don't use `ReflectionHelper` utilities
   - Don't use `DynamicTypeAssembly`
   - Don't use DataKey reflection utilities
   - Avoid `IQueryable.AsQueryable()` extension methods

2. **Use Safe Features:**
   - ✅ Extension methods (string, collection, etc.)
   - ✅ Business exception types
   - ✅ Value objects and entities
   - ✅ Email validator
   - ✅ Async utilities (`AsyncLazy`, `AsyncDisposable`)

3. **Preserve the Assembly (If Needed):**

   If you must use reflection features, add to your `.csproj`:

   ```xml
   <ItemGroup>
     <TrimmerRootAssembly Include="Ark.Tools.Core" />
   </ItemGroup>
   ```

   **Warning:** This will preserve the entire assembly, losing trimming benefits.

### For New Applications

Consider using alternatives for reflection-heavy scenarios:

- **Object Mapping:** Use AutoMapper, Mapster, or source generators
- **Type Discovery:** Use explicit registration instead of assembly scanning
- **DataTable Conversion:** Use typed DTOs with source-generated serialization
- **Dynamic Types:** Use anonymous types or tuples instead of Reflection.Emit

---

## Conclusion

Ark.Tools.Core is **intentionally marked as not trimmable** because:

1. **Fundamental Design:** The library provides reflection-based utilities by design
2. **High Effort, Low Return:** Making it trimmable would require 40-60 hours of breaking changes
3. **Marginal Impact:** 83% of libraries (35/42) are already trimmable despite Core dependency
4. **Clear Alternative Path:** Applications can avoid reflection features or preserve the assembly

### Impact Summary

- **Common Libraries:** 35/42 (83%) trimmable ✅ **MISSION ACCOMPLISHED**
- **Core NOT Trimmable:** Expected and acceptable trade-off
- **Total Achievement:** 42/50 libraries (84%) trimmable across all categories

Applications that **avoid Core's reflection features** can still achieve **30-40% size reduction** from trimming.

---

## References

- [Microsoft: Trimming Warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings)
- [Microsoft: Prepare Libraries for Trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [Trimming Progress Tracker](../../../docs/trimmable-support/progress-tracker.md)
