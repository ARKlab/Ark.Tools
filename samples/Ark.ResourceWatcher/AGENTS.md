# AI Agent Instructions for Ark.ResourceWatcher Sample

## About This Sample

Ark.ResourceWatcher sample demonstrates ETL pipeline patterns using:
- **ResourceWatcher Framework** - Worker host pattern for processing external resources
- **Reqnroll BDD Testing** - Behavior-driven development with Gherkin feature files
- **MSTest + AwesomeAssertions** - Testing framework with Microsoft.Testing.Platform
- **Target Framework** - .NET 10.0

## Critical Rules

**MUST:**
- Use structured logging with NLog - NEVER use string interpolation in log messages
- Always use `CultureInfo.InvariantCulture` when formatting strings for logging
- Use `IArkFlurlClientFactory` instead of `IFlurlClientFactory`
- Add XML documentation (`/// <summary>`) for all public APIs
- Run `dotnet build` after making changes to verify compilation
- Run `dotnet test` after making changes to ensure tests pass
- Work in small, tested increments - make one logical change at a time, build and test before proceeding
- Use `AwesomeAssertions` for test assertions
- Follow Conventional Commits for all commit messages
- Follow SOLID and KISS principles

**MUST NOT:**
- Use `FluentAssertions` (deprecated) - use `AwesomeAssertions` instead
- Use string interpolation in NLog calls (e.g., `_logger.Info($"...")`)
- Use `IFlurlClientFactory` directly - use `IArkFlurlClientFactory`
- Skip XML documentation on public members
- Ignore compiler warnings (TreatWarningsAsErrors is enabled)
- Apply too many changes at once - break work into small, testable increments

## Build & Test Commands

### Prerequisites
- .NET SDK 10.0.101 (specified in `global.json`)

### Basic Commands
```bash
# From sample directory (samples/Ark.ResourceWatcher)
dotnet restore
dotnet build --no-restore
dotnet test

# Run specific test project
dotnet test Ark.ResourceWatcher.Sample.Tests/
```

### Running Single Tests
```bash
# Run specific test by name (supports wildcards)
dotnet test --filter "DisplayName~NewBlob"

# Run tests with specific Reqnroll tag
dotnet test --filter "TestCategory=BlobWorkerHost"
dotnet test --filter "TestCategory=NewBlob"

# Run all tests in a feature
dotnet test --filter "FullyQualifiedName~BlobWorkerHostSteps"

# Combine filters with AND
dotnet test --filter "TestCategory=BlobWorkerHost&DisplayName~processed"

# Run without rebuilding
dotnet test --no-build

# With verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Code Style & Conventions

### File Headers
All source files must include the standard copyright header:
```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
```

### Naming Conventions
- **Private/Protected fields**: `_camelCase` with underscore prefix
- **Public members**: `PascalCase`
- **Interfaces**: `IPascalCase`
- **Local variables**: `camelCase`

```csharp
private readonly ILogger _logger;
private readonly IFlurlClient _client;
public string ResourceId { get; set; }
public interface IMyResourceProcessorConfig { }
```

### var Usage
- **Prefer `var`** for local variables where type is apparent
- Use explicit types for method return types, properties, fields, and parameters

```csharp
// ✅ GOOD - Use var for locals
var transformer = new MyTransformService(resourceId);
var records = csv.GetRecords<SinkRecord>().ToList();

// ✅ GOOD - Explicit types for members
public SinkDto Transform(byte[] input) { ... }
private readonly MockProviderApi _mockApi;
```

### Formatting Rules
- **Indentation**: 4 spaces (no tabs)
- **Braces**: Allman style (new line before opening brace)
- **Line endings**: CRLF
- **Using directives**: Place outside namespace
- **Namespace declarations**: Use file-scoped namespaces (C# 10+)

```csharp
// ✅ GOOD - Allman braces
public void Process(MyResource file, CancellationToken ctk = default)
{
    if (file.Data.Length > 0)
    {
        Transform(file.Data);
    }
}

// ✅ GOOD - File-scoped namespace (C# 10+)
namespace Ark.ResourceWatcher.Sample;

public class MyProcessor
{
    // class members
}
```

### Logging Best Practices
**CRITICAL**: Always use `CultureInfo.InvariantCulture` with structured parameters

```csharp
// ❌ BAD - String interpolation
_logger.Info($"Processing {resourceId}");

// ❌ BAD - Missing CultureInfo
_logger.Info("Processing {ResourceId}", resourceId);

// ✅ GOOD - Structured logging with CultureInfo
_logger.Info(CultureInfo.InvariantCulture, 
    "Processing blob {ResourceId} ({Size} bytes)", 
    file.Metadata.ResourceId, file.Data.Length);
```

### Error Handling
```csharp
// ❌ BAD - Generic exceptions
throw new Exception("Transform failed");

// ✅ GOOD - Specific exception types
throw new ArgumentNullException(nameof(input));
throw new TransformException($"CSV parsing error: {ex.Message}", ex);
throw new InvalidOperationException("Sink API rejected the payload");
```

## ResourceWatcher Patterns

### Worker Host Structure
Inherit from `WorkerHost<TResource, TMetadata, TFilter>` and configure dependencies:

```csharp
public sealed class MyWorkerHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter>
{
    public MyWorkerHost(MyWorkerHostConfig config) : base(config)
    {
        // Register common dependencies
        Use(d =>
        {
            d.Container.RegisterInstance<IArkFlurlClientFactory>(new ArkFlurlClientFactory());
            d.Container.RegisterInstance<IClock>(SystemClock.Instance);
        });

        // Configure data provider
        UseDataProvider<MyStorageResourceProvider>(d =>
        {
            d.Container.RegisterInstance<IMyStorageResourceProviderConfig>(config);
        });

        // Add processor(s)
        AppendFileProcessor<MyResourceProcessor>(d =>
        {
            d.Container.RegisterInstance<IMyResourceProcessorConfig>(config);
        });

        // Configure state provider
        UseStateProvider<InMemStateProvider>();
    }
}
```

### Resource Processor Pattern
Implement `IResourceProcessor<TResource, TMetadata>`:

```csharp
public sealed class MyResourceProcessor : IResourceProcessor<MyResource, MyMetadata>
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IFlurlClient _client;

    public MyResourceProcessor(IArkFlurlClientFactory clientFactory, IMyResourceProcessorConfig config)
    {
        _client = clientFactory.Get(config.SinkUrl);
    }

    public async Task Process(MyResource file, CancellationToken ctk = default)
    {
        _logger.Info(CultureInfo.InvariantCulture, "Processing {ResourceId}", 
            file.Metadata.ResourceId);
        
        // Transform and send
        var transformer = new MyTransformService(file.Metadata.ResourceId);
        var data = transformer.Transform(file.Data);
        await _client.Request("data").PostJsonAsync(data, cancellationToken: ctk);
    }
}
```

### Resource Provider Pattern
Implement `IResourceProvider<TMetadata, TResource, TFilter>`:

```csharp
public Task<IEnumerable<MyMetadata>> GetMetadata(BlobQueryFilter filter, CancellationToken ctk = default)
{
    // Return list of available resources
}

public Task<MyResource?> GetResource(MyMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default)
{
    // Fetch and return resource content
}
```

## Testing Guidelines

### Reqnroll BDD Testing
- Feature files in `Features/` directory with `.feature` extension
- Step definitions in `Steps/` directory  
- Use `[Binding]` attribute on step definition classes
- Use regex patterns for step matching: `[Given(@"pattern")]`, `[When(@"pattern")]`, `[Then(@"pattern")]`
- Use horizontal table format for test data in feature files

### Test Context Pattern
Inject test context into step definition classes:

```csharp
[Binding]
public sealed class BlobWorkerHostSteps
{
    private readonly BlobTestContext _context;

    public BlobWorkerHostSteps(BlobTestContext context)
    {
        _context = context;
    }

    [Given(@"a blob ""(.*)"" exists with checksum ""(.*)"" and content:")]
    public void GivenABlobExists(string blobId, string checksum, string content)
    {
        var now = _context.Clock.GetCurrentInstant().InUtc().LocalDateTime;
        _context.ProviderApi.AddBlob(blobId, content, checksum, now);
    }

    [When(@"the worker runs one cycle")]
    public async Task WhenTheWorkerRunsOneCycle()
    {
        await _context.WorkerHost.RunOnceAsync(ctk: default);
    }

    [Then(@"the blob ""(.*)"" should be processed")]
    public void ThenTheBlobShouldBeProcessed(string blobId)
    {
        _context.ProviderApi.FetchCalls.Should().Contain(blobId);
    }
}
```

### Mock API Testing Pattern
Use in-memory mock APIs to simulate external dependencies:

```csharp
// Mock Provider API - simulates blob storage
public sealed class MockProviderApi
{
    private readonly Dictionary<string, MockBlob> _blobs = new();
    public IReadOnlyList<string> FetchCalls { get; }
    public IReadOnlyList<string> ListCalls { get; }
    
    public void AddBlob(string resourceId, string content, string? checksum, LocalDateTime modified)
    {
        _blobs[resourceId] = new MockBlob(resourceId, content, checksum, modified);
    }
    
    public IEnumerable<MyMetadata> ListBlobs()
    {
        _listCalls.Add(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        return _blobs.Values.Select(b => new MyMetadata { ResourceId = b.ResourceId, ... });
    }
    
    public MyResource? GetBlob(string resourceId)
    {
        _fetchCalls.Add(resourceId);
        return _blobs.TryGetValue(resourceId, out var blob) ? CreateResource(blob) : null;
    }
    
    public void Reset()
    {
        _blobs.Clear();
        _listCalls.Clear();
        _fetchCalls.Clear();
    }
}

// Mock Sink API - simulates destination endpoint
public sealed class MockSinkApi
{
    public IReadOnlyList<SinkDto> ReceivedPayloads { get; }
    public int TotalRecordsReceived => _receivedPayloads.Sum(p => p.Records?.Count ?? 0);
    
    public bool Receive(SinkDto payload)
    {
        if (_failCount > 0) { _failCount--; return false; }
        _receivedPayloads.Add(payload);
        return true;
    }
    
    public void FailNextCalls(int count) => _failCount = count;  // Simulate failures
    public void AlwaysFail() => _alwaysFail = true;
    public void Reset() { _receivedPayloads.Clear(); _failCount = 0; _alwaysFail = false; }
}
```

**Key Pattern**: Mock APIs track calls and provide methods to simulate failures for testing error handling.

### AwesomeAssertions
Use AwesomeAssertions for test assertions:

```csharp
using AwesomeAssertions;

// Collection assertions
_context.ProviderApi.FetchCalls.Should().Contain(blobId);
_context.SinkApi.ReceivedPayloads.Should().HaveCount(1);

// Value assertions
_context.SinkApi.TotalRecordsReceived.Should().Be(expectedCount);
result.Should().NotBeNull();

// Object property assertions
result!.ResultType.Should().Be(ResultType.Normal);
result!.ProcessType.Should().Be(ProcessType.NothingToDo);
```

## XML Documentation

All public types and members require XML documentation:

```csharp
/// <summary>
/// Processor that transforms blob content and sends it to a sink API.
/// </summary>
public sealed class MyResourceProcessor : IResourceProcessor<MyResource, MyMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MyResourceProcessor"/> class.
    /// </summary>
    /// <param name="clientFactory">The Flurl client factory.</param>
    /// <param name="config">The processor configuration.</param>
    public MyResourceProcessor(IArkFlurlClientFactory clientFactory, IMyResourceProcessorConfig config)
    {
        _client = clientFactory.Get(config.SinkUrl);
    }

    /// <inheritdoc/>
    public async Task Process(MyResource file, CancellationToken ctk = default)
    {
        // Implementation
    }
}
```

## Git Commit Guidelines

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope]: <description>
```

**Types**: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert

**Examples:**
```
feat(ResourceWatcher): add CSV transformation processor
fix(Processor): handle empty blob content correctly
test(BDD): add scenario for updated blob detection
docs(Sample): update testing guidelines
refactor(Transform): simplify CSV parsing logic
```

**Guidelines:**
- Use imperative, present tense: "add" not "added"
- Don't capitalize first letter
- No period at end
- Keep description under 50 characters
- Use body to explain "why" vs "what" (when necessary)
