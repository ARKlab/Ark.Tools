# Trimming Guidelines and Best Practices

This document provides guidelines and patterns for making Ark.Tools libraries trim-compatible.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Common Patterns](#common-patterns)
3. [Warning Types and Solutions](#warning-types-and-solutions)
4. [Testing Strategy](#testing-strategy)
5. [When to Suppress](#when-to-suppress)
6. [Anti-Patterns](#anti-patterns)

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

## When to Suppress

### ✅ Safe to Suppress When

1. **Type is statically known via generics**
   - Generic type parameter with constraint
   - Concrete type in code

2. **Type is explicitly registered**
   - Hardcoded type list
   - Compile-time type reference

3. **Framework guarantees preservation**
   - Attribute-based discovery (e.g., [JsonSerializable])
   - Known framework patterns

### ❌ Unsafe to Suppress When

1. **Dynamic type discovery**
   - Assembly scanning
   - Type.GetType(string)

2. **Unknown types at compile time**
   - User-provided type names
   - Configuration-based types

3. **Third-party library constraints**
   - Library requires reflection
   - No control over type registration

### Suppression Template

Always include a detailed justification:

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Explain WHY this is trim-safe. Example: The type T is known at compile time because [reason]. The trimmer will preserve [what] because [how].")]
```

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

## Library Splitting Guidance

### When to Consider Splitting

1. **Small trim-unsafe portion** of otherwise trim-safe library
2. **Optional features** that require reflection
3. **Clear separation** between core and advanced features

### How to Split

1. Create new project for trim-unsafe code
2. Move trim-unsafe types to new project
3. Original library depends on new library (not vice versa)
4. Document which package to use when

### Example Structure

```
Ark.Tools.Core (trimmable)
├── Basic utilities
├── Extension methods
└── Simple helpers

Ark.Tools.Core.Reflection (not trimmable)
├── Reflection utilities
├── Dynamic type handling
└── Assembly scanning
```

---

## Additional Resources

- [Microsoft: Prepare libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [Trim warnings reference](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings)
- [DynamicallyAccessedMembers](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute)
- [JSON source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
