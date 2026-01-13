# Trimming Compatibility - Ark.Tools.Solid.SimpleInjector

## Status: ❌ NOT TRIMMABLE

**Decision Date:** 2026-01-11  
**Rationale:** Fundamentally uses dynamic invocation for handler dispatch that cannot be made trim-safe without breaking changes

---

## Why This Library Is Not Trimmable

Ark.Tools.Solid.SimpleInjector is a handler dispatch library that uses the C# `dynamic` keyword and runtime type construction to invoke command/query handlers. These patterns are fundamentally incompatible with assembly trimming.

### Technical Analysis

#### 1. **Dynamic Handler Invocation** - C# `dynamic` Keyword

**Location:** `Ex.cs` (handler dispatch methods)  
**Warning Types:** IL2026, IL2067

```csharp
// Resolves handler type at runtime
var handlerType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);

// Invokes handler method using dynamic keyword
dynamic handler = container.GetInstance(handlerType);
var result = await handler.ExecuteAsync((dynamic)query, cancellationToken);
```

**Why Not Trimmable:**
- The `dynamic` keyword performs runtime method resolution
- The trimmer cannot determine which methods will be called
- Methods might be trimmed away, causing runtime failures

#### 2. **Runtime Type Construction** - MakeGenericType

**Location:** `Ex.cs` (handler type resolution)  
**Warning Types:** IL2055

```csharp
// Constructs generic handler type at runtime
typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
typeof(ICommandHandler<>).MakeGenericType(commandType);
```

**Why Not Trimmable:**
- Handler types are determined at runtime based on request types
- The trimmer cannot know which `IRequestHandler<,>` or `ICommandHandler<>` implementations to preserve
- Generic type arguments come from runtime values, not compile-time analysis

#### 3. **SimpleInjector Resolution** - Container Lookup

**Location:** `Ex.cs` (handler resolution)  
**Warning Types:** IL2026

```csharp
// Resolves handler from DI container at runtime
var handler = container.GetInstance(handlerType);
```

**Why Not Trimmable:**
- Handlers are resolved by type at runtime
- SimpleInjector uses reflection to create instances
- The trimmer cannot trace which types are registered in the container

---

## Warning Summary

When `IsTrimmable` and `EnableTrimAnalyzer` are enabled, the library produces multiple IL2026, IL2055, and IL2067 warnings from:

1. **Dynamic Invocation:**
   - `dynamic handler = ...`
   - `handler.ExecuteAsync((dynamic)query, ...)`

2. **Generic Type Construction:**
   - `MakeGenericType(queryType, resultType)`
   - `MakeGenericType(commandType)`

3. **Reflection-Based Resolution:**
   - `container.GetInstance(handlerType)`

---

## Alternative Approaches Considered

### ❌ Replace `dynamic` with Explicit Reflection

**Proposal:** Use `MethodInfo.Invoke` instead of `dynamic` keyword

```csharp
// Instead of dynamic invocation
var method = handlerType.GetMethod("ExecuteAsync");
var result = await (Task<TResult>)method.Invoke(handler, new[] { query, cancellationToken });
```

**Why Not:**
- Still requires `DynamicallyAccessedMembers` attributes
- More verbose and harder to maintain
- Doesn't eliminate the fundamental trimming issue
- Breaking changes required to generic constraints
- No practical benefit over current approach

### ❌ Require Explicit Handler Registration

**Proposal:** Force consumers to register handlers explicitly instead of using generic types

**Why Not:**
- **Breaking Change:** Current API allows generic resolution
- **Defeats Purpose:** Library is designed to abstract handler dispatch
- **Consumer Burden:** Every handler type would need manual registration
- **No Migration Path:** Existing code would break

### ❌ Use Source Generators

**Proposal:** Generate handler dispatch code at compile time

**Why Not:**
- **Major Refactoring:** Complete rewrite of the library
- **Limited Benefit:** Library is rarely used in trim-sensitive scenarios
- **Breaking Changes:** Would change the entire API surface
- **High Effort:** Weeks of development for marginal benefit

---

## Comparison to Similar Libraries

### Ark.Tools.Solid.Authorization (Also NOT Trimmable)

Similar pattern - uses `dynamic` for authorization resource handler invocation:

```csharp
// Ex.cs line 67-71 in Solid.Authorization
dynamic handler = container.GetInstance(handlerType);
return await handler.GetResourceAsync((dynamic)query, cancellationToken);
```

**Same Issues:**
- Dynamic invocation
- Runtime type construction
- DI container resolution

### Ark.Tools.Solid (Base - TRIMMABLE ✅)

The base `Ark.Tools.Solid` library **is trimmable** because it only defines:
- Interfaces (`ICommand`, `IQuery<TResult>`, `ICommandHandler<>`, `IRequestHandler<,>`)
- Abstract base classes
- No dynamic invocation

**Lesson:** Interface definitions are trim-safe; runtime dispatch is not.

---

## Impact on Dependent Applications

### Applications Using SimpleInjector Dispatch

If your application uses `Ark.Tools.Solid.SimpleInjector`:

```csharp
// Current usage pattern
var result = await container.ExecuteQueryAsync(query, cancellationToken);
```

**Impact:**
- ❌ Cannot enable trimming for the application
- ❌ Library requires dynamic handler resolution
- ❌ No workaround available without refactoring

### Alternative: Use Trimmable Patterns

If you need trimming support, consider:

1. **Direct Handler Invocation:**

```csharp
// Instead of dynamic dispatch via container
var handler = serviceProvider.GetRequiredService<IRequestHandler<MyQuery, MyResult>>();
var result = await handler.ExecuteAsync(query, cancellationToken);
```

2. **Explicit Handler Registration:**

```csharp
// Register handlers explicitly by type
services.AddScoped<IRequestHandler<GetUserQuery, User>, GetUserHandler>();
```

3. **Minimal APIs (For Web Applications):**

```csharp
// Use ASP.NET Core Minimal APIs instead
app.MapGet("/users/{id}", async (int id, GetUserHandler handler) =>
{
    var result = await handler.ExecuteAsync(new GetUserQuery(id), cancellationToken);
    return Results.Ok(result);
});
```

---

## Recommendations

### For New Applications

If you're starting a new application that requires trimming:

1. **Don't Use This Library:**
   - Use direct dependency injection instead
   - Inject handlers explicitly by type
   - Use Minimal APIs for web applications

2. **Use Base Ark.Tools.Solid:**
   - Define commands/queries using `ICommand` and `IQuery<>`
   - Implement handlers with `ICommandHandler<>` and `IRequestHandler<,>`
   - Inject and invoke handlers directly
   - **Benefit:** All trim-safe, no dynamic dispatch

3. **Example:**

```csharp
// Trim-safe pattern using base Solid library
public class UserController
{
    private readonly IRequestHandler<GetUserQuery, User> _getUserHandler;
    
    public UserController(IRequestHandler<GetUserQuery, User> handler)
    {
        _getUserHandler = handler;
    }
    
    public async Task<User> GetUser(int id, CancellationToken ct)
    {
        var query = new GetUserQuery(id);
        return await _getUserHandler.ExecuteAsync(query, ct);
    }
}
```

### For Existing Applications

If you have an existing application using this library:

1. **Accept No Trimming:**
   - Acknowledge that trimming is not supported
   - Focus on other optimizations (R2R, tiered compilation)

2. **Gradual Migration (If Needed):**
   - Identify handlers used in hot paths
   - Refactor to explicit injection one handler at a time
   - Keep legacy handlers using dynamic dispatch
   - Eventually remove Solid.SimpleInjector dependency

3. **Cost-Benefit Analysis:**
   - Trimming saves 30-40% deployment size
   - Refactoring effort: High (weeks to months for large codebases)
   - Decide if size reduction justifies refactoring cost

---

## When to Use This Library

Despite not being trimmable, this library is still valuable for:

### ✅ Good Use Cases

1. **Non-Trimmed Applications:**
   - Traditional ASP.NET Core MVC applications
   - Applications where deployment size is not a concern
   - Internal enterprise applications

2. **Rapid Development:**
   - Prototypes and MVPs
   - When developer productivity matters more than deployment size
   - Projects with many command/query handlers

3. **Legacy Codebases:**
   - Applications already using this pattern
   - Where refactoring cost outweighs trimming benefits

### ❌ Avoid For

1. **Trimmed Deployments:**
   - Applications targeting WebAssembly
   - Microservices with size constraints
   - Mobile/edge deployments

2. **Native AOT:**
   - Applications using Native AOT compilation
   - Performance-critical scenarios needing ahead-of-time compilation

---

## Conclusion

Ark.Tools.Solid.SimpleInjector is **intentionally marked as not trimmable** because:

1. **Fundamental Design:** Uses C# `dynamic` keyword for handler dispatch by design
2. **Runtime Type Construction:** Handler types built using `MakeGenericType` at runtime
3. **No Non-Breaking Path:** Making it trimmable would require breaking API changes
4. **Clear Alternative:** Use base `Ark.Tools.Solid` with explicit handler injection for trim-safe scenarios

### Impact Summary

- **Base Solid Library:** ✅ Trimmable (interfaces and abstractions)
- **SimpleInjector Integration:** ❌ Not Trimmable (dynamic dispatch)
- **Total Common Libraries:** 35/42 (83%) trimmable ✅

Applications needing trimming support should use **explicit handler injection** instead of dynamic dispatch.

---

## References

- [Microsoft: Trimming Warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-warnings)
- [Microsoft: Dynamic Keyword and Trimming](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types#dynamic-type)
- [Ark.Tools.Solid (Base Library)](../Ark.Tools.Solid/) - Trimmable ✅
- [Ark.Tools.Solid.Authorization](../Ark.Tools.Solid.Authorization/README_TRIMMING.md) - Also NOT Trimmable
- [Trimming Progress Tracker](../../../docs/trimmable-support/progress-tracker.md)
