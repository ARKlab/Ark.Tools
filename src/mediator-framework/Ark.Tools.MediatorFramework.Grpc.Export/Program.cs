// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 2)
    throw new ArgumentException("Expected target assembly and destination directory.");

var targetAssemblyPath = Path.GetFullPath(args[0]);
var destination = Path.GetFullPath(args[1]);
var targetAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(targetAssemblyPath);
var generatedType = targetAssembly.GetType("Ark.MediatorFramework.Generated.ArkGeneratedProtos");
var getFiles = generatedType?.GetMethod("GetFiles", BindingFlags.Public | BindingFlags.Static);
if (getFiles?.Invoke(null, null) is not Array files || files.Length == 0)
    return;

foreach (var file in files)
{
    var fileName = (string)file.GetType().GetField("Item1")!.GetValue(file)!;
    var content = (string)file.GetType().GetField("Item2")!.GetValue(file)!;
    WriteTextFile(destination, fileName, content);
}

WriteEmbeddedAsset(destination, "ark/nodatime.proto", "Ark.Tools.Nodatime.Protobuf");
WriteEmbeddedAsset(destination, "ark/mediator.proto", "Ark.Tools.MediatorFramework.Grpc");

static void WriteTextFile(string destination, string relativePath, string content)
{
    var output = GetSafeOutputPath(destination, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, content);
}

static void WriteEmbeddedAsset(string destination, string relativePath, string assemblyName)
{
    var assembly = Assembly.Load(assemblyName);
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(name => name.EndsWith(relativePath.Replace('/', '.'), StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Embedded protobuf asset '{relativePath}' was not found.");
    using var stream = assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Embedded protobuf asset '{relativePath}' was not found.");
    var output = GetSafeOutputPath(destination, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    using var file = File.Create(output);
    stream.CopyTo(file);
}

static string GetSafeOutputPath(string destination, string relativePath)
{
    if (Path.IsPathRooted(relativePath))
        throw new InvalidOperationException($"Generated protobuf asset path '{relativePath}' must be relative.");

    var fullDestination = Path.GetFullPath(destination);
    var output = Path.GetFullPath(Path.Combine(fullDestination, relativePath));
    if (!output.StartsWith(fullDestination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Generated protobuf asset path '{relativePath}' escapes the destination.");
    return output;
}
