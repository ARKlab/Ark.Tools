# Ark.ResourceWatcher Sample

This sample demonstrates how to build a resource monitoring worker service using Ark.Tools.ResourceWatcher libraries.

## Features

- Worker service for monitoring and processing resources
- File system watching with change detection
- HTTP-based resource fetching
- CSV data processing
- Comprehensive BDD tests using Reqnroll

## Using as a Template

This sample is designed to be used as a template for new resource watcher projects. Follow these steps to "eject" it from the Ark.Tools repository:

### Ejection Steps

1. **Copy the sample folder** to your new repository location:
   ```bash
   cp -r samples/Ark.ResourceWatcher /path/to/your/new/project
   cd /path/to/your/new/project
   ```

2. **Update Ark.Tools package versions** in `Directory.Packages.props`:
   - Change all Ark.Tools package versions from `999.9.9` to the actual version you want to use (e.g., `6.0.0`)
   - Check [NuGet.org](https://www.nuget.org/packages?q=Ark.Tools.ResourceWatcher) for the latest stable version
   
   Example:
   ```xml
   <!-- Before -->
   <PackageVersion Include="Ark.Tools.ResourceWatcher.WorkerHost.Hosting" Version="999.9.9" />
   
   <!-- After -->
   <PackageVersion Include="Ark.Tools.ResourceWatcher.WorkerHost.Hosting" Version="6.0.0" />
   ```

3. **Remove the NuGet.config** or update it:
   - The `NuGet.config` file references a local package source (`../../packages`) that won't exist in your new repository
   - Either delete the file to use the global NuGet sources, or update it to remove the local source:
   ```bash
   rm NuGet.config
   # or edit it to remove the LocalPackages source
   ```

4. **Remove the import from Directory.Build.targets**:
   - In `Directory.Build.targets`, remove the import statement that references the parent directory

5. **Customize for your use case**:
   - Update the worker logic in `Ark.ResourceWatcher.Sample` to match your resource monitoring needs
   - Modify the CSV processing or add new resource types (HTTP, FTP, SQL, etc.)
   - Update tests in `Ark.ResourceWatcher.Sample.Tests` to match your scenarios

6. **Initialize git and commit**:
   ```bash
   git init
   git add .
   git commit -m "Initial commit from Ark.ResourceWatcher template"
   ```

7. **Restore and build**:
   ```bash
   dotnet restore
   dotnet build
   dotnet test
   ```

### What This Sample Includes

- **Ark.ResourceWatcher.Sample**: Worker service application with resource monitoring
- **Ark.ResourceWatcher.Sample.Tests**: BDD tests using Reqnroll

### Key Technologies

- .NET 8.0 and .NET 10.0 (multi-targeting)
- Ark.Tools.ResourceWatcher for resource monitoring
- CsvHelper for CSV parsing
- NLog for logging
- Reqnroll for BDD testing

## Development Setup

1. Configure your resource sources in `appsettings.json`
2. Run the worker:
   ```bash
   cd Ark.ResourceWatcher.Sample
   dotnet run
   ```

## Testing

Run all tests:
```bash
dotnet test
```

The tests use Reqnroll (SpecFlow successor) for behavior-driven development. Feature files are located in the `Features/` directory.

## Resource Types Supported

The ResourceWatcher framework supports multiple resource types:

- **File System**: Monitor local or network file paths
- **HTTP/HTTPS**: Fetch resources from web endpoints
- **FTP/SFTP**: Monitor FTP servers
- **SQL Server**: Watch database tables for changes
- **Azure Blob Storage**: Monitor blob containers

See the [Ark.Tools.ResourceWatcher documentation](https://github.com/ARKlab/Ark.Tools) for more details on each resource type.

## Learn More

- [Ark.Tools Documentation](https://github.com/ARKlab/Ark.Tools)
- [Ark.Tools.ResourceWatcher on NuGet](https://www.nuget.org/packages?q=Ark.Tools.ResourceWatcher)
