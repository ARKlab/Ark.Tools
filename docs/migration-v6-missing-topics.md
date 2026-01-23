# Migration v6 - Potentially Missing Topics

This document lists topics that may need to be added to the migration-v6.md guide. **Please review and confirm which ones should be documented.**

## ✅ Already Covered

The following are already well-documented:
- .NET 10 support (in release notes)
- Trimmable libraries (release notes + Core.Reflection split in migration)
- SLNX adoption
- MTPv2 adoption
- Newtonsoft deprecation
- CPM (Central Package Management)
- Ensure.That removal
- ResourceWatcher type-safe extensions
- CQRS sync methods removal
- Oracle CommandTimeout change
- All test tooling updates

## 🤔 Potentially Missing Topics

### 1. Prerequisites & System Requirements

**Status**: Mentioned in release notes but not migration guide

**Suggested addition**:
```markdown
## Prerequisites for v6

### Required
- .NET SDK 10.0.102 or later (specified in global.json)
- Visual Studio 2022 17.11+ or Rider 2024.3+ (for full .NET 10 support)

### Recommended
- Visual Studio 2022 17.13+ (for SLNX support)
- Docker Desktop (for running integration tests with SQL Server/Azurite)

### Target Framework Migration
Update your application projects:
```xml
<!-- Before (v5) -->
<TargetFramework>net8.0</TargetFramework>

<!-- After (v6) - choose one or both -->
<TargetFramework>net8.0</TargetFramework>  <!-- .NET 8 LTS only -->
<!-- OR -->
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>  <!-- Multi-target -->
<!-- OR -->
<TargetFramework>net10.0</TargetFramework>  <!-- .NET 10 only -->
```

**Note**: Ark.Tools packages multi-target net8.0 and net10.0. Your application can use either framework.
```

**Question**: Should this be added? Where in the document?

---

### 2. Trimming Support for User Applications

**Status**: Trimming is mentioned but no clear guide for users to enable it

**Suggested addition**:
```markdown
## Enable Trimming in Your Application (Optional)

If you want to enable trimming for your own application (not required to use Ark.Tools):

1. **Add to your executable project (.csproj)**:
```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>link</TrimMode>
  
  <!-- Optional: Enable Native AOT -->
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

2. **Test thoroughly**: Trimming can break reflection-based code
3. **Review warnings**: Address IL2XXX warnings from the trimmer
4. **See**: [Trimming Guidelines](trimmable-support/guidelines.md) for details

**Known limitations with Ark.Tools**:
- `Ark.Tools.Core.Reflection` namespace is NOT trimmable (by design)
- Most other Ark.Tools packages are trim-compatible
- Methods using reflection are marked with `[RequiresUnreferencedCode]`
```

**Question**: Should this be added as a new optional section?

---

### 3. NuGet Package Version Compatibility

**Status**: Not explicitly documented

**Suggested addition**:
```markdown
## NuGet Package Versions

### Breaking Version Changes
- All Ark.Tools.* packages bump to v6.0.0
- You must upgrade ALL Ark.Tools packages together to v6.x
- Mixing v5 and v6 packages is NOT supported

### Third-Party Package Updates
Major dependency updates in v6:
- Swashbuckle.AspNetCore: 10.x (from 6.x in v5)
- Reqnroll: 2.x (replaces Specflow 3.x)
- AwesomeAssertions: 9.x (replaces FluentAssertions)
- Microsoft.Testing.Platform: 2.x (optional, for MTPv2)

See Directory.Packages.props in samples for exact versions.
```

**Question**: Should version information be added?

---

### 4. Performance Improvements

**Status**: Not documented

**Potential addition**:
```markdown
## Performance Improvements in v6

- **System.Text.Json**: 2-3x faster serialization vs Newtonsoft.Json
- **Trimming**: Smaller deployment size (can reduce by 40-60% with aggressive trimming)
- **Native AOT**: Faster cold start times (experimental)
- **MTPv2**: Faster test execution and better parallel test support
- **.NET 10**: General runtime performance improvements
```

**Question**: Should performance improvements be highlighted? Do we have specific benchmarks?

---

### 5. Ark.ResourceWatcher Sample Project

**Status**: Samples exist but not mentioned in migration

**Potential addition**:
```markdown
## ResourceWatcher Sample Project

A new comprehensive sample project is available in `samples/Ark.ResourceWatcher/`:

- **Ark.ResourceWatcher.Sample**: Complete implementation example
- **Ark.ResourceWatcher.Sample.Tests**: Integration tests with Reqnroll

This replaces the older TestWorker sample and demonstrates:
- Type-safe extension usage
- Blob storage integration
- State tracking
- Error handling patterns

See the [ResourceWatcher documentation](resourcewatcher.md) for details.
```

**Question**: Should the sample project be highlighted in the migration guide?

---

### 6. API Changes in Specific Packages

**Status**: Need to verify if there are other API changes not documented

**Potential areas to check**:
- Ark.Tools.Sql.* - Any breaking changes in SQL packages?
- Ark.Tools.Http - Any breaking changes?
- Ark.Tools.Rebus - Changes beyond what's in v5 migration?
- Ark.Tools.AspNetCore.* - Beyond Newtonsoft removal?

**Question**: Should we do a comprehensive API diff between v5 and v6?

---

### 7. Deprecation Warnings

**Status**: Not documented

**Potential addition**:
```markdown
## Deprecated Features

The following features are deprecated in v6 and will be removed in v7:

### Packages Marked as Deprecated
- None currently (but Newtonsoft support will be removed in v7)

### Methods Marked as Obsolete
- `ICommandProcessor.Execute()` - Marked with `[Obsolete(error: true)]`
- `IQueryProcessor.Execute()` - Marked with `[Obsolete(error: true)]`
- `IRequestProcessor.Execute()` - Marked with `[Obsolete(error: true)]`

These throw `NotSupportedException` if called. Use `ExecuteAsync()` instead.
```

**Question**: Should deprecations be explicitly listed?

---

### 8. CI/CD Pipeline Changes

**Status**: MTPv2 CI changes mentioned briefly

**Potential expansion**:
```markdown
## CI/CD Pipeline Updates

### Azure DevOps
When adopting MTPv2, update your pipeline YAML:

```yaml
# Before (VSTest)
- task: VSTest@2
  inputs:
    testSelector: 'testAssemblies'
    testAssemblyVer2: '**/*Tests.dll'

# After (dotnet test with MTPv2)
- task: DotNetCoreCLI@2
  displayName: 'Run tests'
  inputs:
    command: 'test'
    projects: '$(solutionPath)'
    arguments: '--configuration $(BuildConfiguration) --no-build --no-restore --report-trx --coverage --crashdump --crashdump-type mini --hangdump --hangdump-timeout 10m --hangdump-type mini --minimum-expected-tests 1'
    publishTestResults: true
```

### GitHub Actions
```yaml
# Similar update for GitHub Actions
- name: Test
  run: dotnet test --configuration Release --no-build --no-restore --report-trx --coverage
```

**Question**: Should CI/CD updates be expanded?

---

### 9. Breaking Changes in Database/ORM

**Status**: Oracle timeout is documented, but other data access changes?

**Potential areas**:
- Dapper version changes?
- Entity Framework integration (if any)?
- SQL Server specific changes?
- Connection string format changes?

**Question**: Are there any other database-related breaking changes?

---

### 10. Security/Vulnerability Fixes

**Status**: NuGet Audit mentioned but not specific fixes

**Potential addition**:
```markdown
## Security Enhancements

- **NuGet Audit**: Now enabled by default for all projects
- **Updated Dependencies**: All dependencies updated to latest secure versions
- **Removed Unmaintained Libraries**: Ensure.That, Nito.AsyncEx (security through maintenance)
- **Trimming Security**: Reduced attack surface with trimmed deployments
```

**Question**: Should security improvements be highlighted?

---

## Summary

**Confirmed covered topics**: 16 major changes
**Potentially missing topics**: 10 areas identified above

**Recommendation**: Review the above topics and confirm which ones should be added to migration-v6.md. Most are optional enhancements rather than critical missing information, but they may help users understand the full scope of v6 changes.

## Next Steps

Please review and indicate:
1. ✅ **Add this** - Topic should be documented
2. ❌ **Skip this** - Not needed
3. ℹ️ **More info needed** - Need to verify/research

I can then update the migration-v6.md document accordingly.
