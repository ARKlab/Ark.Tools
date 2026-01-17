# ResourceWatcher Test Refactoring Plan

## Current State
The test step definitions have code duplication across multiple step files:
- `SqlStateProviderSteps.cs` (382 LOC)
- `TypeSafeExtensionsSteps.cs` (578 LOC)  
- `StateTransitionsSteps.cs` (463 LOC)

## Pattern to Follow

Based on `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Tests/`:

### 1. State Classes with Public Properties
**Example:** `BookSteps.cs`
```csharp
[Binding]
public sealed class BookSteps
{
    private readonly TestClient _client;
    
    // Exposed for other step classes to inject and use
    public Book.V1.Output? Current { get; private set; }
    
    public BookSteps(TestClient client)
    {
        _client = client;
    }
}
```

### 2. Step Classes Inject Dependencies
**Example:** `BookPrintProcessSteps.cs`
```csharp
[Binding]
public sealed class BookPrintProcessSteps
{
    private readonly TestClient _client;
    private readonly BookSteps _bookSteps;  // Inject other step class
    
    public BookPrintProcessSteps(TestClient client, BookSteps bookSteps)
    {
        _client = client;
        _bookSteps = bookSteps;
    }
    
    [Given(@"I have created a book print process for that book")]
    public void GivenIHaveCreatedABookPrintProcess()
    {
        // Reuse state from BookSteps
        var request = new BookPrintProcess.V1.Create
        {
            BookId = _bookSteps.Current!.Id  // Access public property
        };
    }
}
```

### 3. Shared Context/Helper Classes
**Example:** `TestClient.cs` - marked with `[Binding]` for DI
```csharp
[Binding]
public sealed class TestClient
{
    // Common HTTP verbs reusable across all steps
    public void Get(string requestUri) { }
    public void PostAsJson(string uri, object body) { }
    public void ThenTheRequestSucceded() { }
}
```

## Duplication to Consolidate

### Common Database Setup (3 duplicates)
- `NodaTimeDapper.Setup()`
- Database connection initialization
- `_getUniqueTenant()` helper

**Solution:** Created `SqlStateProviderContext` with:
- `InitializeDatabase()` verb
- `GetUniqueTenant(string baseTenant)` verb

### State Management Patterns
Each step class has:
- `_currentState` / `_currentVoidState` / `_currentTypedState`
- `_statesToSave` / `_voidStatesToSave` / `_typedStatesToSave`
- `_loadedStates` / `_loadedVoidStates` / `_loadedTypedStates`

**Solution (blocked by Reqnroll limitation):**
Cannot create generic `ResourceStateContext<TExtensions>` with `[Binding]` attribute.
Reqnroll throws: "Binding types cannot be generic"

**Alternative approaches:**
1. Create non-generic derived classes per extension type
2. Use manual registration in hooks
3. Keep state in step classes but extract common verbs to static helpers

### Assertion Helpers (duplicated across all 3 files)
- Finding resources by ID
- Asserting Modified/CheckSum/RetryCount values
- Loading and filtering state collections

**Solution:** Extract to `CommonStepHelpers` (already exists, extend it)

## Recommended Refactoring Steps

### Phase 1: Extract Common Setup (Low Risk) ✅
- [x] Create `SqlStateProviderContext` for DB initialization
- [x] Create `DbSetupLock` for thread-safe schema setup
- [ ] Update all step classes to inject `SqlStateProviderContext`
- [ ] Replace duplicated `GivenASqlServerDatabaseIsAvailable()` with context call

### Phase 2: Consolidate Assertions (Medium Risk)
- [ ] Extend `CommonStepHelpers` with assertion verbs
- [ ] Extract resource finding logic
- [ ] Create shared "Then" step definitions for common assertions

### Phase 3: State Sharing (High Risk)
- [ ] Expose `Current` properties in step classes
- [ ] Inject step classes into each other where needed
- [ ] Remove duplicated state initialization code

### Phase 4: Extract Reusable Verbs (Medium Risk)
- [ ] Create `ResourceStateHelper` for save/load operations
- [ ] Consolidate extension manipulation logic
- [ ] Standardize ModifiedSource handling

## Benefits

1. **Reduced LOC:** ~1500 LOC → ~800 LOC (estimated 46% reduction)
2. **Better Testability:** Shared contexts can be tested independently
3. **Easier Maintenance:** Changes to common patterns in one place
4. **Clearer Intent:** Step classes focus on their specific domain
5. **Reusability:** New features can leverage existing verbs

## Constraints

- Reqnroll doesn't support generic `[Binding]` classes
- Must maintain backward compatibility with existing feature files
- Tests must continue passing during incremental refactoring
- Follow existing patterns from `samples/` directory

## Next Steps

1. Get approval on refactoring approach (incremental vs. big bang)
2. Start with Phase 1 (SqlStateProviderContext usage)
3. Add integration tests for shared contexts
4. Document the Driver pattern for future contributors
