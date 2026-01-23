# Ark.Tools v6.0 Release Notes

Ark.Tools v6.0 is a major release focusing on modernization, performance, and trimming support for .NET applications. This release includes breaking changes that require migration from v5.

## 🎯 Key Highlights

### .NET 10 Support
- **Multi-targeting**: .NET 8.0 LTS and .NET 10.0
- Full support for latest .NET features and performance improvements
- Requires .NET SDK 10.0.102 (specified in global.json)

### Trimming & Native AOT Support
- **100% Trimmable Libraries**: All 61 Ark.Tools packages are now trim-compatible
- Enables Native AOT compilation for smaller deployment sizes and faster startup
- Reflection-based utilities properly annotated with `RequiresUnreferencedCode`
- See [Trimming Guidelines](trimmable-support/guidelines.md) for details

### Modern Development Tooling
- **SLNX Support**: New Visual Studio solution format (`Ark.Tools.slnx`)
- **Central Package Management (CPM)**: Unified dependency management across solution
- **Microsoft Testing Platform v2 (MTPv2)**: Modern test runner with better performance
- **SDK-based SQL Projects**: Improved SQL Server project integration

### System.Text.Json First
- System.Text.Json is now the default serializer in AspNetCore packages
- Better performance, native trimming support, and source generation capabilities
- Newtonsoft.Json still supported but requires explicit configuration
- **Breaking Change**: `useNewtonsoftJson` parameter removed from startup classes

## 🔨 Breaking Changes

### CQRS Handler Interfaces
- **Removed**: Synchronous `Execute()` methods from all CQRS handlers
- **Impact**: Only async `ExecuteAsync()` methods are supported
- **Migration**: Remove sync implementations, make calling code async
- **Benefit**: Eliminates sync-over-async patterns that cause deadlocks

### Newtonsoft.Json in AspNetCore
- **Removed**: Built-in Newtonsoft.Json support from `Ark.Tools.AspNetCore`
- **Default**: System.Text.Json is now the only built-in serializer
- **Migration**: Either migrate to System.Text.Json or add Newtonsoft.Json manually
- **Benefit**: Reduced dependencies, better performance, native trimming

### ResourceWatcher Type Safety
- **Changed**: Generic type parameters for compile-time type safety
- **Impact**: Extensions property is now strongly typed
- **Migration**: Use proxy classes (minimal changes) or strongly-typed extensions
- **Benefit**: IntelliSense support, compile-time errors, better AoT compatibility

### Oracle CommandTimeout
- **Changed**: Default timeout from 0 (infinite) to 30 seconds
- **Impact**: Long-running queries may timeout
- **Migration**: Explicitly set `CommandTimeout` for queries >30 seconds
- **Benefit**: Prevents runaway queries, aligns with SQL Server defaults

### TypeConverter Registration (.NET 9+)
- **New Requirement**: Types used as dictionary keys must be registered
- **Impact**: Applications targeting .NET 9+ need `TypeDescriptor.RegisterType<T>()`
- **When**: Only affects custom types with `TypeConverterAttribute` used as dictionary keys
- **Benefit**: Native AOT compatibility, trim-safe type discovery

## ✨ New Features & Enhancements

### Dependencies Modernized
- **Removed Ensure.That**: Replaced with built-in null checks and ArgumentNullException.ThrowIfNull
- **Removed Nito.AsyncEx.Coordination**: Replaced with built-in `AsyncLazy<T>` in Core library
- **Benefits**: Fewer dependencies, lighter footprint, modern .NET patterns

### Testing Improvements
- **AwesomeAssertions**: Replaces deprecated FluentAssertions
- **Reqnroll**: Modern BDD framework (Specflow successor)
- **MTPv2**: Microsoft Testing Platform v2 with better performance and features
- **Better IDE Integration**: Hot reload, crash dumps, hang dumps, retry support

### Code Quality Enhancements
- **Updated .editorconfig**: Latest code style rules
- **Enhanced Analyzers**: .NET analyzers, Meziantou analyzers
- **NuGet Audit**: Automatic vulnerability scanning
- **Deterministic Builds**: Reproducible build outputs

### Swashbuckle 10.x
- Updated to latest OpenAPI 3.1 support
- Breaking change in security requirements configuration
- See migration guide for SecurityRequirementsOperationFilter changes

## 📦 Package Changes

### Core Libraries
- All packages now support .NET 8.0 and .NET 10.0
- All packages are trimmable (marked with `<IsTrimmable>true</IsTrimmable>`)
- Central Package Management enabled

### AspNetCore Packages
- Default to System.Text.Json serialization
- Newtonsoft.Json support requires manual configuration
- Updated to ASP.NET Core 8.0/10.0

### ResourceWatcher Packages
- Type-safe extensions with generic parameters
- Backward-compatible proxy classes for non-extension users
- Internal AsyncLazy implementation (no external dependency)

## 🚀 Migration Path

### Recommended Steps

1. **Review Breaking Changes**: Read [Migration Guide](migration-v6.md) carefully
2. **Update Target Framework**: Ensure .NET 8.0+ in project files
3. **Fix CQRS Handlers**: Remove synchronous Execute methods
4. **Handle Serialization**: Choose System.Text.Json or configure Newtonsoft.Json
5. **Update Tests**: Migrate to AwesomeAssertions, Reqnroll, MTPv2
6. **Adopt Modern Tooling** (Optional): CPM, SLNX, editorconfig updates
7. **Enable Trimming** (Optional): Add `<IsTrimmable>true</IsTrimmable>` to your projects

### Optional Modernizations

The following changes are adopted in the Ark.ReferenceProject samples but are **not required** for library users:

- **Central Package Management**: Manage versions in Directory.Packages.props
- **SLNX Format**: New solution file format
- **MTPv2**: Microsoft Testing Platform v2
- **Updated Analyzers**: Latest code quality rules
- **Trimming**: Enable trimming for your applications

These are demonstrated in the samples as best practices but you can adopt them at your own pace.

## 📚 Resources

- **[Migration Guide](migration-v6.md)**: Detailed migration instructions
- **[Trimming Guidelines](trimmable-support/guidelines.md)**: Comprehensive trimming documentation
- **[Ark.ReferenceProject](../samples/Ark.ReferenceProject/)**: Complete example implementation
- **[README](../README.md)**: Getting started guide

## 🙏 Acknowledgments

Special thanks to all contributors who helped with this major release, particularly in achieving 100% trimming support across all packages.

## 📝 Version Information

- **Release Date**: TBD
- **Target Frameworks**: .NET 8.0, .NET 10.0
- **SDK Version**: 10.0.102
- **NuGet Packages**: Available on nuget.org

For detailed changes and migration instructions, see the [Migration Guide](migration-v6.md).
