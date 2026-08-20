// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 2)
    throw new ArgumentException("Expected target assembly and destination directory.");

var targetAssemblyPath = Path.GetFullPath(args[0]);
var destination = Path.GetFullPath(args[1]);
var targetDirectory = Path.GetDirectoryName(targetAssemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var dependencyPath = Path.Combine(targetDirectory, name.Name + ".dll");
    return File.Exists(dependencyPath)
        ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependencyPath)
        : null;
};
var targetAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(targetAssemblyPath);
var generatedType = targetAssembly.GetType("Ark.Tools.MediatorFramework.Generated.ArkGeneratedProtos")
    ?? targetAssembly.GetType("Ark.Tools.MediatorFramework.Generated.ArkGeneratedEndpoints+ArkGeneratedProtos");
var getFiles = generatedType?.GetMethod("GetFiles", BindingFlags.Public | BindingFlags.Static);
if (getFiles?.Invoke(null, null) is not Array files || files.Length == 0)
    return;

foreach (var file in files)
{
    var fileName = (string)file.GetType().GetField("Item1")!.GetValue(file)!;
    var content = (string)file.GetType().GetField("Item2")!.GetValue(file)!;
    await WriteTextFileAsync(destination, fileName, content).ConfigureAwait(false);
}

await WriteEmbeddedAssetAsync(destination, "ark/nodatime.proto", "Ark.Tools.Nodatime.Protobuf").ConfigureAwait(false);
await WriteEmbeddedAssetAsync(destination, "ark/mediator.proto", "Ark.Tools.MediatorFramework.Grpc").ConfigureAwait(false);
await File.WriteAllTextAsync(Path.Combine(destination, ".ark-export-active"), string.Empty).ConfigureAwait(false);

static async Task WriteTextFileAsync(string destination, string relativePath, string content)
{
    var output = GetSafeOutputPath(destination, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, content).ConfigureAwait(false);
}

static async Task WriteEmbeddedAssetAsync(string destination, string relativePath, string assemblyName)
{
    var assembly = Assembly.Load(assemblyName);
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(name => name.EndsWith(relativePath.Replace('/', '.'), StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Embedded protobuf asset '{relativePath}' was not found.");
    var stream = assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Embedded protobuf asset '{relativePath}' was not found.");
    var output = GetSafeOutputPath(destination, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    var file = File.Create(output);
    await using (stream.ConfigureAwait(false))
    await using (file.ConfigureAwait(false))
    {
        await stream.CopyToAsync(file).ConfigureAwait(false);
    }
}

static string GetSafeOutputPath(string destination, string relativePath)
{
    if (Path.IsPathRooted(relativePath))
        throw new InvalidOperationException($"Generated protobuf asset path '{relativePath}' must be relative.");

    var fullDestination = Path.GetFullPath(destination);
    var output = Path.GetFullPath(Path.Combine(fullDestination, relativePath));
    var relativeOutput = Path.GetRelativePath(fullDestination, output);
    if (relativeOutput == ".."
        || relativeOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || Path.IsPathRooted(relativeOutput))
        throw new InvalidOperationException($"Generated protobuf asset path '{relativePath}' escapes the destination.");
    return output;
}
