# Trimming Guidelines and Best Practices

This document provides guidelines and patterns for making Ark.Tools libraries trim-compatible.

## Core Philosophy

### Primary Goal: Make as Much as Possible Trimmable

The goal of this initiative is to **make as many libraries trimmable as feasible**. 

**Key Insight from 100% Achievement:** Libraries with reflection code CAN be trimmable by using `RequiresUnreferencedCode` to propagate warnings to users. A library is trimmable as long as it builds with zero warnings - even if methods are marked with `RequiresUnreferencedCode`.

### When to Mark a Library as NOT Trimmable

It is acceptable to leave a library as not trimmable only in rare cases:

1. **Third-party dependencies are fundamentally incompatible** with trimming and cannot be isolated
2. **The complexity/effort to annotate outweighs the benefits** (use `RequiresUnreferencedCode` liberally instead)
3. **The library is a development tool** rarely used in trim-sensitive deployment scenarios

**Before marking as NOT trimmable:** Try using `RequiresUnreferencedCode` on public APIs. Most reflection-heavy code can be made trimmable this way.

**When in doubt, make it trimmable with `RequiresUnreferencedCode`** rather than marking the entire library as not trimmable.

## Table of Contents

1. [Lessons Learned from 100% Achievement](#lessons-learned-from-100-achievement)
2. [Quick Start](#quick-start)
3. [Common Patterns](#common-patterns)
4. [Warning Types and Solutions](#warning-types-and-solutions)
5. [Testing Strategy](#testing-strategy)
6. [When to Suppress (CRITICAL)](#when-to-suppress-critical)
7. [Anti-Patterns](#anti-patterns)

---

## Lessons Learned from 100% Achievement

In January 2026, we achieved 100% trimmable libraries (61/61) across all Ark.Tools packages. Here are the key lessons:

### 1. Libraries with Reflection CAN Be Trimmable ✅

**Misconception:** "If a library uses reflection, it cannot be trimmable."

**Reality:** A library is trimmable as long as it builds with **zero warnings**. Methods can be marked with `[RequiresUnreferencedCode]` to propagate warnings to users.

**Example:**
```csharp
// This library IS trimmable even though it uses reflection
public static class ReflectionHelper
{
    [RequiresUnreferencedCode("Uses reflection to inspect type interfaces.")]
    public static Type? GetEnumerableItemType(Type type)
    {
        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && 
                i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }
}
```

### 2. Default to Propagating Warnings, Not Suppressing

**Key Principle:** Use `RequiresUnreferencedCode` as the primary approach for reflection code.

- ✅ **DO**: Use `[RequiresUnreferencedCode]` on public APIs that use reflection
- ✅ **DO**: Let warnings propagate to users who can decide if trimming is suitable
- ❌ **DON'T**: Use `UnconditionalSuppressMessage` to hide warnings unless genuinely safe

### 3. Merging Is Better Than Splitting

**Previous Approach:** Split reflection code into separate packages (e.g., Core vs Core.Reflection)

**Better Approach:** Keep code together with `RequiresUnreferencedCode` annotations

**Benefits:**
- Simpler package structure for users
- Same namespace, easier migration
- Users get clear warnings about reflection usage
- Backward compatible

**When Learned:** Successfully merged Core.Reflection back into Core with zero warnings

### 4. 100% Is Achievable with Proper Annotations

**Achievement:** 61/61 libraries (100%) made trimmable in 4 hours

**Keys to Success:**
- Use `RequiresUnreferencedCode` liberally on public APIs
- Use `DynamicallyAccessedMembers` on generic type parameters
- Use `UnconditionalSuppressMessage` sparingly with detailed justifications
- Test thoroughly after each change

### 5. Follow Microsoft Guidelines Strictly

**Critical Rules:**
- Default to propagating warnings with `RequiresUnreferencedCode`
- Only suppress with `UnconditionalSuppressMessage` when genuinely safe
- Provide detailed justifications for all suppressions
- Always run tests after enabling trimming

**Reference:** [Microsoft's Prepare Libraries for Trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)

---

## Quick Start

### Enabling Trimming for a Library

1. **Add configuration to .csproj:**
   ```xml
   <PropertyGroup>
     <IsTrimmable>true</IsTrimmable>
     <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
   </PropertyGroup>
   ```

2. **Restore packages with force re-evaluation:**
   ```bash
   dotnet restore --force-evaluate
   ```
   
   ⚠️ **Critical**: When adding `IsTrimmable` and `EnableTrimAnalyzer`, the build system automatically adds `Microsoft.NET.ILLink.Tasks` package reference. You **must** run `dotnet restore --force-evaluate` to update package lock files, otherwise the build will fail with NU1004 errors.

3. **Build and check for warnings:**
   ```bash
   dotnet build --no-restore
   ```
   
   ⚠️ **Important**: Run the full build (not just `--no-restore` alone) to ensure `EnableTrimAnalyzer` properly activates and catches all trim warnings. Building without a proper restore may not detect warnings.

4. **Fix or suppress warnings** (see sections below)

5. **Add tests** for trim-sensitive code paths

6. **Run all tests to verify no regressions:**
   ```bash
   dotnet test --no-build --no-restore
   ```
   
   ⚠️ **Required**: Always run the full test suite after enabling trimming to ensure no functionality is broken. Even if a library builds with zero warnings, it may have runtime issues that only tests will catch.

### Common Pitfalls to Avoid

❌ **Don't skip the restore step** - Package lock files must be updated when trimming properties are added

❌ **Don't assume zero build warnings means success** - Always run tests to verify runtime behavior

❌ **Don't use `--no-restore` during initial testing** - This may hide trim warnings from `EnableTrimAnalyzer`

---

## Common Patterns

### Pattern 1: Generic Base Classes

**Use Case**: Type converters, handlers where type is known at compile time

**Example** (from Ark.Tools.Nodatime):
```csharp
// Generic base class - single suppression point
public class NullableNodaTimeConverter<T> : NullableConverter
    where T : struct
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The generic type parameter T is known at compile time for each concrete instantiation, making the underlying nullable type T? statically discoverable and trim-safe.")]
    public NullableNodaTimeConverter() : base(typeof(T?))
    {
    }
}

// Concrete implementations - no suppression needed
public class NullableLocalDateConverter : NullableNodaTimeConverter<LocalDate> { }
public class NullableInstantConverter : NullableNodaTimeConverter<Instant> { }
```

**Benefits**:
- Single suppression point
- Type-safe implementation
- Easy to maintain and extend

### Pattern 2: DynamicallyAccessedMembers Attribute

**Use Case**: Methods that use reflection on type parameters

**Example**:
```csharp
public class EventHandler<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TEvent>
{
    public void Handle(TEvent evt)
    {
        // Can safely use reflection on TEvent methods
        var methods = typeof(TEvent).GetMethods();
        // ...
    }
}
```

**Attribute Options**:
- `PublicMethods` - For GetMethods()
- `PublicProperties` - For GetProperties()
- `PublicConstructors` - For GetConstructors()
- `All` - When you need everything (use sparingly)

### Pattern 3: Explicit Type Registration

**Use Case**: Dapper type handlers, DI registrations

**Example**:
```csharp
// Instead of dynamic type discovery
public static void RegisterHandlers()
{
    // ❌ Not trim-safe
    // var handlers = Assembly.GetExecutingAssembly()
    //     .GetTypes()
    //     .Where(t => typeof(IHandler).IsAssignableFrom(t));
    
    // ✅ Trim-safe
    RegisterHandler<LocalDateHandler>();
    RegisterHandler<InstantHandler>();
    // ... explicitly list all handlers
}
```

### Pattern 4: Source Generators for JSON

**Use Case**: System.Text.Json serialization

**Example**:
```csharp
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MyDto))]
[JsonSerializable(typeof(List<MyDto>))]
internal partial class MyJsonContext : JsonSerializerContext
{
}

// Usage
var json = JsonSerializer.Serialize(dto, MyJsonContext.Default.MyDto);
```

---

## Warning Types and Solutions

### IL2026: RequiresUnreferencedCode

**Common Sources**:
- `NullableConverter` constructor
- `TypeDescriptor.GetConverter(Type)`
- JSON serialization methods
- Reflection-based APIs

**Solutions**:

1. **Use generics** to make type statically known
2. **Suppress with justification** when type is genuinely known
3. **Use source generators** for JSON
4. **Explicit type registration** instead of discovery

**Example Suppression**:
```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "The type LocalDate is statically known and will not be trimmed.")]
public void ProcessLocalDate()
{
    var converter = TypeDescriptor.GetConverter(typeof(LocalDate));
}
```

### IL2070: GetInterfaces

**Common in**: Event sourcing, message handling

**Solution**: Add `DynamicallyAccessedMembers` attribute

```csharp
public bool ImplementsInterface<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>()
{
    return typeof(T).GetInterfaces().Any(i => i == typeof(IMyInterface));
}
```

### IL2075-IL2076: DI Container Warnings

**Common in**: SimpleInjector, other DI frameworks

**Solutions**:
1. Use explicit registration instead of assembly scanning
2. Add attributes to generic parameters
3. Suppress if the container guarantees type preservation

### IL2090: GetMethods

**Common in**: Reflection-heavy code, proxies

**Solution**: Add `DynamicallyAccessedMembers.PublicMethods`

```csharp
public void InvokeMethods<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>()
{
    var methods = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Instance);
    // ...
}
```

---

## Testing Strategy

### Critical Testing Requirements

⚠️ **Always run the full test suite** after enabling trimming for a library. Even if the build succeeds with zero warnings, trimming can cause runtime failures that only tests will catch.

**Required test command:**
```bash
dotnet test --no-build --no-restore
```

### Why Full Testing is Essential

1. **Build warnings only catch compile-time issues** - Many trimming problems only manifest at runtime
2. **Package lock file changes** - The `Microsoft.NET.ILLink.Tasks` package addition may affect dependencies
3. **Behavioral verification** - Tests ensure that trimmed code still behaves correctly
4. **Regression detection** - Existing functionality must not break when trimming is enabled

### Test Categories

1. **Roundtrip Tests** - Verify data integrity
2. **Null Handling Tests** - Verify edge cases
3. **Integration Tests** - Verify with TypeDescriptor/DI

### Example Test Structure

```csharp
[TestClass]
public class ConverterTests
{
    [TestMethod]
    public void Converter_ShouldRoundtripConversion()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(LocalDate?));
        var original = new LocalDate(2024, 1, 10);

        // Act - Convert to string
        var stringValue = converter.ConvertToString(original);
        
        // Convert back from string
        var converted = converter.ConvertFromString(stringValue);

        // Assert
        converted.Should().BeOfType<LocalDate>();
        ((LocalDate)converted!).Should().Be(original);
    }

    [TestMethod]
    public void Converter_ShouldHandleNullValue()
    {
        // Arrange
        var converter = TypeDescriptor.GetConverter(typeof(LocalDate?));
        LocalDate? nullValue = null;

        // Act
        var stringValue = converter.ConvertToString(nullValue);

        // Assert
        stringValue.Should().Be(string.Empty);
        converter.ConvertFromString(string.Empty).Should().BeNull();
    }
}
```

### Test Outcomes to Verify

✅ **Do test**:
- Actual behavior (conversion, serialization)
- Roundtrip fidelity
- Null/edge case handling
- Real-world usage patterns (TypeDescriptor, DI)

❌ **Don't test**:
- Implementation details (base class type)
- Internal properties (UnderlyingType)
- Framework internals

---

## When to Suppress (CRITICAL)

⚠️ **READ THIS SECTION CAREFULLY** - Incorrect use of `UnconditionalSuppressMessage` can hide real trimming bugs.

### Microsoft's Guidance on UnconditionalSuppressMessage

Per [Microsoft's official documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming#unconditionalsuppressmessage):

> **When suppressing warnings, you are responsible for guaranteeing the trim compatibility of the code based on invariants that you know to be true by inspection and testing. Use caution with these annotations, because if they are incorrect, or if invariants of your code change, they might end up hiding incorrect code.**

**Key Principle from Microsoft**: "It's only valid to suppress a warning if there are annotations or code that ensure the reflected-on members are visible targets of reflection. It isn't sufficient that the member was a target of a call, field, or property access."

**Critical Rule**: `UnconditionalSuppressMessage` should **ONLY** be used when:
1. The code is **genuinely safe** despite the warning
2. The intent **cannot be expressed** with `RequiresUnreferencedCode` or `DynamicallyAccessedMembers` attributes
3. You can **prove by inspection and testing** that the code will work correctly when trimmed
4. The trimmer cannot prove safety statically, but you have invariants ensuring correctness

**Warning from Microsoft**: Properties, fields, and methods that aren't visible targets of reflection could be:
- Inlined
- Have their names removed  
- Get moved to different types
- Otherwise be optimized in ways that break reflection

This means suppressions can become incorrect as trimming optimizations evolve.

### ✅ Safe to Suppress When

#### 1. **Private initialization with public propagation**

When warnings are properly propagated through public APIs:

```csharp
public static ArkDefaultJsonSerializerSettings Instance
{
    [RequiresUnreferencedCode("JSON serialization might require unreferenced types.")]
    get => _instance;  // ✅ Warning propagated here
}

[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "The singleton instance is created here but warnings are propagated through the Instance property getter.")]
private static readonly ArkDefaultJsonSerializerSettings _instance = new();  // ✅ Safe - warning propagated above
```

#### 2. **Type is statically known via generics**

Generic type parameter with constraint ensures type is known at compile time:

```csharp
public class Converter<T> where T : struct
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The generic type parameter T is known at compile time for each concrete instantiation, making the underlying type statically discoverable and trim-safe.")]
    public Converter() : base(typeof(T))
    {
    }
}
```

#### 3. **Type is explicitly registered and annotated**

When types are explicitly listed and proper annotations ensure preservation:

```csharp
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public Type this[int i]
{
    [UnconditionalSuppressMessage("Trimming", "IL2063",
        Justification = "The list only contains types stored through the annotated setter which ensures constructors are preserved.")]
    get => types[i];  // ✅ Safe - setter annotation guarantees constructor preservation
    set => types[i] = value;
}
```

#### 4. **Framework guarantees preservation**

When using framework patterns that guarantee type preservation:

```csharp
// Source-generated JSON context
[JsonSerializable(typeof(MyType))]
[UnconditionalSuppressMessage("Trimming", "IL2026",
    Justification = "JsonSerializable attribute ensures MyType and its properties are preserved.")]
public partial class MyJsonContext : JsonSerializerContext { }
```

#### 5. **Override methods (IL2046 constraint)**

When overriding methods that cannot have `RequiresUnreferencedCode`:

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "TStruct is constrained to value types. The factory ensures TStruct will be preserved.")]
public override TStruct? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
{
    return JsonSerializer.Deserialize<TStruct>(ref reader, options);
}
```

### ❌ Unsafe to Suppress When

#### 1. **Dynamic type discovery without preservation**

```csharp
// ❌ WRONG - Type from string is not preserved
[UnconditionalSuppressMessage("Trimming", "IL2026")]
public object Create(string typeName)
{
    var type = Type.GetType(typeName);  // Trimmer doesn't know what types to keep!
    return Activator.CreateInstance(type);
}
```

#### 2. **Hiding warnings instead of propagating**

```csharp
// ❌ WRONG - Suppressing on public API hides warning from callers
[UnconditionalSuppressMessage("Trimming", "IL2026")]
public void ProcessDynamic(Type type)  // Should use RequiresUnreferencedCode instead!
{
    var converter = TypeDescriptor.GetConverter(type);
    // ...
}
```

#### 3. **Unknown types at compile time**

```csharp
// ❌ WRONG - User-provided type names cannot be statically analyzed
[UnconditionalSuppressMessage("Trimming", "IL2026")]
public void LoadPlugin(string assemblyName, string typeName)
{
    var assembly = Assembly.LoadFrom(assemblyName);
    var type = assembly.GetType(typeName);
    // ...
}
```

#### 4. **Invalid justification based on non-reflective usage**

Per Microsoft's documentation, this is **explicitly invalid**:

```csharp
// ❌ INVALID - Using a property non-reflectively doesn't guarantee reflection will work
[UnconditionalSuppressMessage("Trimming", "IL2063",
    Justification = "*INVALID* Only need to serialize properties that are used by the app. *INVALID*")]
public string Serialize(object o)
{
    foreach (var property in o.GetType().GetProperties())  // ❌ Properties may be trimmed!
    {
        AppendProperty(sb, property, o);
    }
}
```

### Decision Tree: Should I Use UnconditionalSuppressMessage?

```
Is the warning valid?
├─ NO → Use UnconditionalSuppressMessage with detailed justification
│       Example: Generic type known at compile time
│
└─ YES → Can I express the intent with RequiresUnreferencedCode?
    ├─ YES → Use RequiresUnreferencedCode (propagate warning)
    │        Example: Method uses reflection on dynamic types
    │
    └─ NO → Can I express the intent with DynamicallyAccessedMembers?
        ├─ YES → Use DynamicallyAccessedMembers
        │        Example: Type parameter used for reflection
        │
        └─ NO → Is the code genuinely safe despite the warning?
            ├─ YES → Use UnconditionalSuppressMessage
            │        Example: Override method, warning propagated elsewhere
            │
            └─ NO → Refactor code to be trim-safe
                     Example: Replace assembly scanning with explicit types
```

### Suppression Template (REQUIRED)

**ALWAYS include a detailed justification** explaining:
1. **Why** the code is safe despite the warning
2. **What** ensures the types/members are preserved
3. **How** the trimmer will know what to keep

```csharp
[UnconditionalSuppressMessage("Trimming", "IL20XX:WarningCode",
    Justification = "Explain WHY this is trim-safe. State WHAT ensures preservation. Describe HOW the trimmer knows what to keep. Example: The type T is known at compile time through the generic constraint, ensuring the trimmer preserves the necessary members.")]
```

### Common Valid Justifications

| Pattern | Valid Justification Example |
|---------|---------------------------|
| Generic with known type | "The generic type parameter T is known at compile time for each concrete instantiation, making the type statically discoverable and trim-safe." |
| Private field with public propagation | "The singleton instance is created here but warnings are propagated through the Instance property getter." |
| Annotated collection | "The collection only contains types stored through the annotated setter which ensures the required members are preserved." |
| Override constraint | "Cannot use RequiresUnreferencedCode on override. The factory/caller is responsible for ensuring type preservation." |
| Source generator | "The [JsonSerializable] attribute ensures this type and its properties are preserved by the trimmer." |

### When in Doubt

**Default to propagating warnings, not suppressing them.**

If you're unsure whether a suppression is safe:
1. Use `RequiresUnreferencedCode` to propagate the warning instead
2. Ask for review with specific details about why you think it's safe
3. Add comprehensive tests to verify behavior with trimming enabled
4. Document the uncertainty in code comments for future review

---

## Anti-Patterns

### ❌ Don't: Suppress Without Justification

```csharp
// BAD
[UnconditionalSuppressMessage("Trimming", "IL2026")]
public void Method() { }
```

### ❌ Don't: Use Assembly Scanning

```csharp
// BAD - Not trim-safe
var types = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(t => typeof(IHandler).IsAssignableFrom(t));

// GOOD - Trim-safe
var handlers = new Type[] { 
    typeof(Handler1), 
    typeof(Handler2) 
};
```

### ❌ Don't: Use Type.GetType(string)

```csharp
// BAD - Type name from configuration
var type = Type.GetType(config.TypeName);

// GOOD - Use a factory or explicit mapping
var type = typeName switch {
    "Handler1" => typeof(Handler1),
    "Handler2" => typeof(Handler2),
    _ => throw new ArgumentException()
};
```

### ❌ Don't: Ignore Warnings Without Investigation

Always investigate warnings before suppressing. They often indicate real trimming issues.

---

## Library Design Principles for Trimming

### Microsoft's Recommendations

Based on [Microsoft's official guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming#recommendations), follow these principles when designing libraries for trimming:

1. **Avoid reflection when possible** - Minimize reflection scope so it's reachable only from a small part of the library

2. **Annotate with DynamicallyAccessedMembers** - Statically express trimming requirements when possible

3. **Reorganize code for analyzability** - Make it follow analyzable patterns that can be annotated

4. **Propagate RequiresUnreferencedCode to public APIs** - When code is incompatible with trimming, annotate it and propagate up to relevant public APIs

5. **Avoid reflection in static constructors** - Using statically unanalyzable reflection in static constructors results in warnings propagating to all members of the class

6. **Avoid annotating virtual/interface methods** - Requires all overrides to have matching annotations

7. **Consider source generators** - For reflection-heavy APIs (like serializers), adopt source generators for better static analysis

### When a Library Should NOT Be Trimmable

**UPDATED GUIDANCE (2026-01-22):** After achieving 100% trimmable libraries, this guidance has changed.

**It's rarely necessary** to mark a library as not trimmable. Most reflection code can be made trimmable using `RequiresUnreferencedCode`.

**Only mark as NOT trimmable when:**
- **Third-party dependencies** are fundamentally not trim-compatible and cannot be isolated
- **The library is a development/build tool** never deployed in trim scenarios

**Document the reason** clearly when deciding not to pursue trimming support.

## Library Splitting Guidance

### ⚠️ UPDATED: Reconsider Before Splitting

**Previous Approach:** Split reflection-heavy code into separate packages.

**Lesson Learned:** Keeping code together with `RequiresUnreferencedCode` is often better.

### When Splitting MAY Be Appropriate

Consider splitting only when **ALL** of these are true:

1. **Third-party dependency conflict:** The reflection features require incompatible third-party packages
2. **Massive code size:** The reflection portion is very large and most users don't need it
3. **Clear user groups:** Distinct user bases with different needs
4. **Cannot use RequiresUnreferencedCode:** The warnings cannot be expressed through attributes

### Prefer Merging with Annotations

**Instead of splitting, use this approach:**

1. Keep all code in one package
2. Mark reflection methods with `[RequiresUnreferencedCode]`
3. Users get warnings when using reflection features
4. Users can suppress warnings if they know types are preserved

**Benefits of NOT Splitting:**
- ✅ Simpler package structure (one package, not two)
- ✅ Same namespace, easier to use
- ✅ No migration needed for users
- ✅ Clear warnings show which features require reflection
- ✅ Backward compatible

### Example: Core.Reflection Merge

Previously split:
```
Ark.Tools.Core (trimmable) ✅
Ark.Tools.Core.Reflection (not trimmable) ❌
```

Successfully merged:
```
Ark.Tools.Core (trimmable) ✅
├── Basic utilities
├── Reflection utilities (marked with RequiresUnreferencedCode)
└── All features in one package
```

**Result:** 100% trimmable, simpler for users, no breaking changes.

---

## Additional Resources

### Official Microsoft Documentation

**Core Concepts:**
- [Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming) - Comprehensive guide for library authors
- [Understanding trim analysis](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-concepts) - How the trimmer analyzes code
- [Fixing trim warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings) - Step-by-step workflows for resolving warnings
- [Trim warnings reference](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings) - Complete list of IL#### warning codes

**Attributes and APIs:**
- [RequiresUnreferencedCodeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.requiresunreferencedcodeattribute) - Mark code as incompatible with trimming
- [UnconditionalSuppressMessageAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.unconditionalsuppressmessageattribute) - Suppress warnings with justification
- [DynamicallyAccessedMembersAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute) - Specify reflection requirements
- [DynamicDependencyAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicdependencyattribute) - Keep specific members (last resort)

**Advanced Topics:**
- [JSON source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) - Trim-safe JSON serialization
- [Native AOT compatibility](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) - AOT analyzers and requirements
- [Intrinsic APIs marked RequiresUnreferencedCode](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/intrinsic-requiresunreferencedcode-apis) - Special cases (MakeGenericMethod, etc.)
- [Known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities) - Framework-level issues
